using Android.Content.PM;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Avalonia.Controls;
using Avalonia.Platform;

namespace XPScript.UI.Android;

/// <summary>CameraX preview adapter. Permission is deliberately checked by the host before creation.</summary>
public sealed class AndroidCameraPreviewControl : NativeControlHost
{
    private PreviewView? _previewView;
    private ProcessCameraProvider? _cameraProvider;
    private ImageCapture? _imageCapture;

    public Task<string> CapturePhotoAsync(string outputPath)
    {
        outputPath = NormalizeCapturePath(outputPath);
        if (_imageCapture is null) throw new InvalidOperationException("Camera preview is not initialized.");
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var file = new Java.IO.File(outputPath);
        var options = new ImageCapture.OutputFileOptions.Builder(file).Build();
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = global::Android.App.Application.Context ?? throw new InvalidOperationException("Android application context is unavailable.");
        _imageCapture.TakePicture(options, ContextCompat.GetMainExecutor(context)!, new PhotoCaptureCallback(completion, outputPath));
        return completion.Task;
    }

    private static string NormalizeCapturePath(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (outputPath.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Camera photo output path may not contain '..'.", nameof(outputPath));
        var root = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var fullPath = Path.GetFullPath(Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(root, outputPath));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Camera photo output path must remain inside the application sandbox.", nameof(outputPath));
        return fullPath;
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context
            ?? throw new InvalidOperationException("Android application context is unavailable.");
        if (global::AndroidX.Core.Content.ContextCompat.CheckSelfPermission(context, global::Android.Manifest.Permission.Camera) != Permission.Granted)
            throw new InvalidOperationException("Camera permission has not been granted.");
        if (context is not ILifecycleOwner lifecycleOwner)
            throw new InvalidOperationException("Android camera preview requires a lifecycle owner.");

        _previewView = new PreviewView(context);
        var future = ProcessCameraProvider.GetInstance(context);
        future.AddListener(new Java.Lang.Runnable(() =>
        {
            _cameraProvider = future.Get() as ProcessCameraProvider
                ?? throw new InvalidOperationException("CameraX provider is unavailable.");
            var preview = new Preview.Builder().Build();
            _imageCapture = new ImageCapture.Builder().Build();
            preview.SetSurfaceProvider(ContextCompat.GetMainExecutor(context)!, _previewView.SurfaceProvider);
            _cameraProvider.UnbindAll();
            _cameraProvider.BindToLifecycle(lifecycleOwner, CameraSelector.DefaultBackCamera, preview, _imageCapture);
        }), ContextCompat.GetMainExecutor(context));

        return new PlatformHandle(_previewView.Handle, "Android.CameraX.PreviewView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _cameraProvider?.UnbindAll();
        _cameraProvider?.Dispose();
        _cameraProvider = null;
        _imageCapture?.Dispose();
        _imageCapture = null;
        _previewView?.Dispose();
        _previewView = null;
        base.DestroyNativeControlCore(control);
    }

    private sealed class PhotoCaptureCallback : Java.Lang.Object, ImageCapture.IOnImageSavedCallback
    {
        private readonly TaskCompletionSource<string> _completion;
        private readonly string _outputPath;
        public PhotoCaptureCallback(TaskCompletionSource<string> completion, string outputPath) { _completion = completion; _outputPath = outputPath; }
        public void OnError(ImageCaptureException exception) => _completion.TrySetException(new InvalidOperationException(exception.Message ?? "Camera photo capture failed."));
        public void OnImageSaved(ImageCapture.OutputFileResults output) => _completion.TrySetResult(_outputPath);
    }
}
