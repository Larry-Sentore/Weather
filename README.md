# Weather-Responsive Environment in Unity

A Unity project that retrieves current weather from OpenWeather and applies the returned conditions to a Tenkoku environment. The scene also displays weather information and supports preset locations and custom coordinates.

## Requirements

- Unity **6000.6.0f1** (see `ProjectSettings/ProjectVersion.txt`)
- An OpenWeather API key with access to the Current Weather API
- Internet access when running the application

## Setup

1. Clone the repository and open its root folder in Unity Hub using Unity 6000.6.0f1.
2. Create `Assets/Resources/WeatherApiKey.txt`. Create the `Assets/Resources` folder if it does not exist.
3. Put only your OpenWeather API key in `WeatherApiKey.txt`.
   - `Assets/Testing/WeatherApiKey.example.txt` is a placeholder/template; it is not a working key.
   - The key file and its Unity `.meta` file are ignored by Git. Do not force-add either file.
4. Open `Assets/Scenes/SampleScene.unity` and press **Play**.

The `Weather` component loads the key as a Unity `Resources` asset named `WeatherApiKey`. If the file is absent or empty, the app displays a missing-key message instead of making a request.

## Using the application

- Select one of the five preset locations: Bujumbura, Brussels, Kwidzyn, Manila, or Hangzhou.
- Or enter latitude and longitude and submit the custom search. Latitude must be between -90 and 90; longitude must be between -180 and 180. Empty, non-numeric, and out-of-range values are rejected with a message.
- A successful response updates the displayed location, temperature in °C, wind speed in m/s, humidity, weather description, and day/night status.
- The weather conditions also update Tenkoku cloud, rain, snow, fog, wind, temperature, humidity, and time settings.

## Implementation overview

The main implementation is in [`Assets/Scripts/Weather.cs`](Assets/Scripts/Weather.cs):

- `WeatherApiResponse` and its nested serializable classes model the OpenWeather JSON fields used by the app.
- `OpenWeatherProvider` sends the HTTP request using `UnityWebRequest`.
- `Weather` validates coordinates and response data, updates the UI, and applies the weather to Tenkoku.
- `IWeatherProvider` separates the request interface from the component that consumes the response. The `WeatherUpdated` event notifies the UI and environment update handlers.
- A new request stops an in-progress request; a request-version check prevents an older response from overwriting a newer selection.

## Testing

Sample responses are provided in [`Assets/Testing`](Assets/Testing):

- [`rain-response.json`](Assets/Testing/rain-response.json)
- [`snow-response.json`](Assets/Testing/snow-response.json)

Useful manual checks include:

1. Select each preset and confirm that the displayed location and weather update.
2. Submit valid custom coordinates.
3. Try blank, non-numeric, and out-of-range coordinates and confirm that validation feedback appears.
4. Run with a missing or invalid API key, or without network access, and confirm that an error is displayed.
5. Test rain and snow conditions and confirm the corresponding Tenkoku effects change.

## Building

The project includes `Assets/Scenes/SampleScene.unity` in its enabled build scenes. In Unity, open **File > Build Profiles**, choose the target platform, and build the project.

Generated builds are ignored by Git. Do not commit generated build output.

