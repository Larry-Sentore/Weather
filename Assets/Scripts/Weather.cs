using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using Tenkoku.Core;

// Requests weather for preset or custom coordinates and updates the weather UI.
public class Weather : MonoBehaviour
{
    public float Bujumbura_lat = -3.36278f;
    public float Bujumbura_lon = 29.36444f;
    public float Brussels_lat = 50.8503f;
    public float Brussels_lon = 4.3517f;
    public float Kwidzyn_lat = 53.7244f;
    public float Kwidzyn_lon = 18.9286f;
    public float Manila_lat = 14.5995f;
    public float Manila_lon = 120.9842f;
    public float Hangzhou_lat = 30.2741f;
    public float Hangzhou_lon = 120.1551f;
    public TMPro.TextMeshProUGUI Location_Text;
    public TMPro.TextMeshProUGUI Temperature_Text;
    public TMPro.TextMeshProUGUI WindSpeed_Text;
    public TMPro.TextMeshProUGUI Humidity_Text;
    public TMPro.TextMeshProUGUI Time_of_Day;
    public TMPro.TextMeshProUGUI Description_Text;

    [SerializeField] private TMPro.TMP_Dropdown locationDropdown;
    [SerializeField] private TMPro.TMP_InputField latitudeInput;
    [SerializeField] private TMPro.TMP_InputField longitudeInput;
    [SerializeField] private UnityEngine.UI.Button customSearchButton;
    [SerializeField] private TenkokuModule tenkokuModule;
    private IWeatherProvider weatherProvider;
    private Coroutine activeRequest;
    private int requestVersion;
    private float requestedLatitude;
    private float requestedLongitude;
    private bool warnedTenkokuDisabled;
    private string apiKey;
    public event Action<WeatherApiResponse> WeatherUpdated;

    // Holds the fields to display the OpenWeather response.
    [Serializable]
    public class WeatherApiResponse
    {
        public string name;
        public WeatherCondition[] weather;
        public MainWeatherData main;
        public WindData wind;
        public SystemData sys;
        public CloudData clouds;
        public long dt;
        public int timezone;
    }

    // Holds the weather description and day/night icon.
    [Serializable]
    public class WeatherCondition
    {
        public int id;
        public string description;
        public string icon;
    }

    // Holds current temperature and humidity.
    [Serializable]
    public class MainWeatherData
    {
        public float temp;
        public int humidity;
    }

    // Holds the current wind speed.
    [Serializable]
    public class WindData
    {
        public float speed;
        public int deg = -1;
    }

    // Holds the cloud coverage percentage from the weather response.
    [Serializable]
    public class CloudData
    {
        public int all;
    }

    // Holds country and local sunrise/sunset timestamps.
    [Serializable]
    public class SystemData
    {
        public string country;
        public long sunrise;
        public long sunset;
    }

    // Sets up the provider and subscribes the UI to weather updates.
    private void Awake()
    {
        TextAsset apiKeyAsset = Resources.Load<TextAsset>("WeatherApiKey");
        apiKey = apiKeyAsset != null ? apiKeyAsset.text.Trim() : string.Empty;
        weatherProvider = new OpenWeatherProvider();
        WeatherUpdated += UpdateWeatherUI;
        WeatherUpdated += ApplyWeatherToTenkoku;
    }

    // Connects the dropdown and custom-coordinate button, then loads Bujumbura.
    private void Start()
    {
        if (tenkokuModule == null)
        {
            tenkokuModule = FindAnyObjectByType<TenkokuModule>(FindObjectsInactive.Include);
        }

        InitializeWeatherUI();
    }

    // Removes event handlers when this component is destroyed.
    private void OnDestroy()
    {
        WeatherUpdated -= UpdateWeatherUI;
        WeatherUpdated -= ApplyWeatherToTenkoku;

        if (locationDropdown != null)
        {
            locationDropdown.onValueChanged.RemoveListener(OnPresetLocationSelected);
        }

        if (customSearchButton != null)
        {
            customSearchButton.onClick.RemoveListener(SubmitCustomCoordinates);
        }
    }

    // Connects UI events and starts with the first preset location.
    private void InitializeWeatherUI()
    {

        locationDropdown.onValueChanged.AddListener(OnPresetLocationSelected);
        customSearchButton.onClick.AddListener(SubmitCustomCoordinates);
        locationDropdown.SetValueWithoutNotify(1);
        OnPresetLocationSelected(1);
    }

    // Requests weather when the user chooses one of the preset locations.
    private void OnPresetLocationSelected(int index)
    {
        switch (index)
        {
            case 1:
                RequestWeather(Bujumbura_lat, Bujumbura_lon, "Bujumbura");
                break;
            case 2:
                RequestWeather(Brussels_lat, Brussels_lon, "Brussels");
                break;
            case 3:
                RequestWeather(Kwidzyn_lat, Kwidzyn_lon, "Kwidzyn");
                break;
            case 4:
                RequestWeather(Manila_lat, Manila_lon, "Manila");
                break;
            case 5:
                RequestWeather(Hangzhou_lat, Hangzhou_lon, "Hangzhou");
                break;
            default:
                ShowMessage("Choose a preset location or enter coordinates.");
                break;
        }
    }

    // Validates the latitude and longitude fields before requesting custom weather.
    private void SubmitCustomCoordinates()
    {
        string latitudeText = latitudeInput.text.Trim();
        string longitudeText = longitudeInput.text.Trim();

        if (string.IsNullOrEmpty(latitudeText))
        {
            ShowMessage("Enter a latitude between -90 and 90.");
            return;
        }

        if (string.IsNullOrEmpty(longitudeText))
        {
            ShowMessage("Enter a longitude between -180 and 180.");
            return;
        }

        if (!float.TryParse(latitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float latitude) ||
            float.IsNaN(latitude) || float.IsInfinity(latitude) || latitude < -90f || latitude > 90f)
        {
            ShowMessage("Latitude must be a number between -90 and 90.");
            return;
        }

        if (!float.TryParse(longitudeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float longitude) ||
            float.IsNaN(longitude) || float.IsInfinity(longitude) || longitude < -180f || longitude > 180f)
        {
            ShowMessage("Longitude must be a number between -180 and 180.");
            return;
        }

        RequestWeather(latitude, longitude, null);
    }

    // Starts a weather request and cancels any previous request in progress.
    private void RequestWeather(float latitude, float longitude, string presetName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ShowMessage("OpenWeather API key is missing. Create Assets/Resources/WeatherApiKey.txt using Assets/Testing/WeatherApiKey.example.txt.");
            return;
        }

        if (activeRequest != null)
        {
            StopCoroutine(activeRequest);
        }

        int currentVersion = ++requestVersion;
        requestedLatitude = latitude;
        requestedLongitude = longitude;
        ResetWeatherUI();
        Location_Text.text = presetName ?? FormatCoordinates(latitude, longitude);

        activeRequest = StartCoroutine(weatherProvider.GetWeatherJson(
            latitude,
            longitude,
            apiKey,
            json =>
            {
                if (currentVersion == requestVersion)
                {
                    activeRequest = null;
                    HandleWeatherResponse(json);
                }
            },
            error =>
            {
                if (currentVersion == requestVersion)
                {
                    activeRequest = null;
                    ShowMessage(error);
                }
            }));
    }

    // Deserializes the response and publishes valid weather data to the UI.
    private void HandleWeatherResponse(string json)
    {
        WeatherApiResponse weather;
        try
        {
            weather = JsonUtility.FromJson<WeatherApiResponse>(json);
        }
        catch (ArgumentException)
        {
            ShowMessage("The weather service returned unreadable data.");
            return;
        }

        if (weather == null || weather.main == null || weather.wind == null ||
            weather.weather == null || weather.weather.Length == 0)
        {
            ShowMessage("The weather response did not contain the required data.");
            return;
        }

        WeatherUpdated?.Invoke(weather);
    }

    // Displays the latest valid weather response in the assigned TextMeshPro fields.
    private void UpdateWeatherUI(WeatherApiResponse weather)
    {
        string location = !string.IsNullOrWhiteSpace(weather.name)
            ? weather.name
            : FormatCoordinates(requestedLatitude, requestedLongitude);
        string country = weather.sys != null && !string.IsNullOrWhiteSpace(weather.sys.country)
            ? $", {weather.sys.country}"
            : string.Empty;

        Location_Text.text = $"{location}{country}";
        Temperature_Text.text = $"{weather.main.temp.ToString("0.#", CultureInfo.InvariantCulture)} °C";
        WindSpeed_Text.text = $"{weather.wind.speed.ToString("0.#", CultureInfo.InvariantCulture)} m/s";
        Humidity_Text.text = $"{weather.main.humidity}%";
        Description_Text.text = !string.IsNullOrWhiteSpace(weather.weather[0].description)
            ? weather.weather[0].description
            : "Weather description unavailable.";
        Time_of_Day.text = GetTimeOfDay(weather);
    }

    // Applies the current API condition and measurements to Tenkoku's manual controls.
    private void ApplyWeatherToTenkoku(WeatherApiResponse weather)
    {
        

        int conditionId = weather.weather[0].id;
        int conditionGroup = conditionId / 100;
        float cloudCoverage = weather.clouds != null
            ? Mathf.Clamp01(weather.clouds.all / 100f)
            : conditionGroup == 8 && conditionId != 800 ? 0.65f : 0f;
        float rainAmount = 0f;
        float snowAmount = 0f;
        float fogAmount = 0f;
        float overcast = cloudCoverage;

        if (conditionGroup == 2)
        {
            rainAmount = 0.9f;
            overcast = Mathf.Max(overcast, 0.75f);
        }
        else if (conditionGroup == 3)
        {
            rainAmount = 0.35f;
            overcast = Mathf.Max(overcast, 0.55f);
        }
        else if (conditionGroup == 5)
        {
            rainAmount = 0.75f;
            overcast = Mathf.Max(overcast, 0.7f);
        }
        else if (conditionGroup == 6)
        {
            snowAmount = 0.75f;
            overcast = Mathf.Max(overcast, 0.65f);
        }
        else if (conditionGroup == 7)
        {
            fogAmount = 0.6f;
            overcast = Mathf.Max(overcast, 0.55f);
        }
        else if (conditionId == 800)
        {
            cloudCoverage = 0f;
            overcast = 0f;
        }

        tenkokuModule.weatherTypeIndex = 0;
        tenkokuModule.weather_setAuto = false;
        tenkokuModule.weather_OvercastAmt = Mathf.Clamp01(overcast);
        tenkokuModule.weather_cloudCumulusAmt = Mathf.Clamp01(cloudCoverage);
        tenkokuModule.weather_cloudAltoStratusAmt = Mathf.Clamp01(overcast);
        tenkokuModule.weather_cloudCirrusAmt = Mathf.Clamp01(cloudCoverage * 0.5f);
        tenkokuModule.weather_RainAmt = rainAmount;
        tenkokuModule.weather_SnowAmt = snowAmount;
        tenkokuModule.weather_FogAmt = fogAmount;
        tenkokuModule.weather_WindAmt = Mathf.Clamp01(weather.wind.speed / 20f);
        tenkokuModule.weather_temperature = Mathf.Clamp(weather.main.temp * 9f / 5f + 32f, 0f, 120f);
        tenkokuModule.weather_humidity = Mathf.Clamp01(weather.main.humidity / 100f);

        if (weather.wind.deg >= 0)
        {
            tenkokuModule.weather_WindDir = weather.wind.deg;
        }

        tenkokuModule.autoTimeSync = false;
        tenkokuModule.autoDateSync = false;
        tenkokuModule.autoTime = false;
        tenkokuModule.useAutoTime = false;

        if (weather.dt > 0)
        {
            DateTime localObservation = DateTimeOffset
                .FromUnixTimeSeconds(weather.dt)
                .UtcDateTime
                .AddSeconds(weather.timezone);
            tenkokuModule.currentHour = localObservation.Hour;
            tenkokuModule.currentMinute = localObservation.Minute;
            tenkokuModule.currentSecond = localObservation.Second;
            tenkokuModule.currentDay = localObservation.Day;
            tenkokuModule.currentMonth = localObservation.Month;
            tenkokuModule.currentYear = localObservation.Year;
        }

        if (!tenkokuModule.enabled && !warnedTenkokuDisabled)
        {
            warnedTenkokuDisabled = true;
            Debug.LogWarning("Weather values were assigned, but TenkokuModule is disabled and will not render them.");
        }
    }

    // Determines local day or night from the API observation and sunrise/sunset data.
    private string GetTimeOfDay(WeatherApiResponse weather)
    {
        if (weather.dt > 0 && weather.sys != null &&
            weather.sys.sunrise > 0 && weather.sys.sunset > 0)
        {
            long observation = weather.dt;
            return observation >= weather.sys.sunrise && observation < weather.sys.sunset
                ? "Daytime"
                : "Nighttime";
        }

        string icon = weather.weather[0].icon;
        if (!string.IsNullOrEmpty(icon) && icon.EndsWith("d", StringComparison.Ordinal))
        {
            return "Daytime";
        }

        if (!string.IsNullOrEmpty(icon) && icon.EndsWith("n", StringComparison.Ordinal))
        {
            return "Nighttime";
        }

        return "Time unavailable";
    }

    // Clears stale values and marks them unavailable while a request is running.
    private void ResetWeatherUI()
    {
        Temperature_Text.text = "-- °C";
        WindSpeed_Text.text = "-- m/s";
        Humidity_Text.text = "--%";
        Time_of_Day.text = "--";
    }

    // Shows a loading, validation, configuration, or request-failure message.
    private void ShowMessage(string message)
    {
        if (Description_Text != null)
        {
            Description_Text.text = message;
        }
    }

    // Formats coordinates consistently regardless of the computer's number locale.
    private string FormatCoordinates(float latitude, float longitude)
    {
        return $"Coordinates: {latitude.ToString("0.#####", CultureInfo.InvariantCulture)}, " +
               longitude.ToString("0.#####", CultureInfo.InvariantCulture);
    }
}

// Abstracts retrieval of the raw weather response from the UI and deserialization.
public interface IWeatherProvider
{
    // Retrieves the weather JSON for the supplied coordinates.
    IEnumerator GetWeatherJson(
        float latitude,
        float longitude,
        string apiKey,
        Action<string> onSuccess,
        Action<string> onError);
}

// Retrieves current weather from OpenWeather using UnityWebRequest.
public sealed class OpenWeatherProvider : IWeatherProvider
{
    // Sends a metric current-weather request and reports success or failure.
    public IEnumerator GetWeatherJson(
        float latitude,
        float longitude,
        string apiKey,
        Action<string> onSuccess,
        Action<string> onError)
    {
        string latitudeText = latitude.ToString(CultureInfo.InvariantCulture);
        string longitudeText = longitude.ToString(CultureInfo.InvariantCulture);
        string url = "https://api.openweathermap.org/data/2.5/weather" +
                     $"?lat={latitudeText}&lon={longitudeText}" +
                     $"&appid={Uri.EscapeDataString(apiKey)}&units=metric";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Weather request failed ({request.responseCode}): {request.error}");
                yield break;
            }

            onSuccess?.Invoke(request.downloadHandler.text);
        }
    }
}
