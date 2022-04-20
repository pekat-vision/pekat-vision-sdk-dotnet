# PEKAT VISION SDK

A simple .NET library for communication with PEKAT VISION.

## Requirements

* .NET Framework or .NET Core with .NET Standard 2.0 support

## Installation

Just compile the sources into the .NET Standard library.

## Usage

Create local analyzer (will start Pekat Vision server in background):

```csharp
using PekatVisionSDK;

Analyzer analyzer = await Analyzer.CreateLocalAnalyzer("/path/to/server/installation", "/path/to/project", "optional api key");
```

Run analysis:

```csharp
Result result = await analyzer.Analyze("/path/to/image.png", ResultType.AnnotatedImage);
// context
string context = result.Context;
// image data
byte[] image = result.Image;
```

You pass path to PNG file and required result type. There is optional last parameter for additional data (string). There is also variant of this
method accepting memory buffer with PNG instead of file. Finally, you can use `AnalyzeRaw` method accepting raw image data and dimensions. There
should by width * height * 3 bytes in buffer. You obtain results from Result object. It contains context and image data, any of them can
be null if not provided by server.

At the end you have to remove the analyzer. This will also destroy the server.

```csharp
await a.DisposeAsync();
```

You can also connect to already running server using:

```csharp
Analyzer analyzer = await Analyzer.CreateRemoteAnalyzer("host", 1234 /* port */, "optional api key");
```
Remote analyzer enables you to connect to a remotely running PEKAT VISION. It is possible to connect from multiple PCs simultaneously. PEKAT VISION behaves as a server automatically.

### Multiple cameras

```csharp
Analyzer analyzer_camera_1 = await Analyzer.CreateLocalAnalyzer("/path/to/server/installation", "/path/to/project_camera_1", "");
Analyzer analyzer_camera_2 = await Analyzer.CreateLocalAnalyzer("/path/to/server/installation", "/path/to/project_camera_2", "");
Analyzer analyzer_camera_3 = await Analyzer.CreateLocalAnalyzer("/path/to/server/installation", "/path/to/project_camera_3", "");

// analyze - loop
Result result_camera_1 = await analyzer_camera_1.Analyze("/path/to/image.png", ResultType.AnnotatedImage);
Result result_camera_2 = await analyzer_camera_2.Analyze("/path/to/image.png", ResultType.AnnotatedImage);
Result result_camera_3 = await analyzer_camera_3.Analyze("/path/to/image.png", ResultType.AnnotatedImage);

```
