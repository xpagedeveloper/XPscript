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
            preview.SetSurfaceProvider(ContextCompat.GetMainExecutor(context)!, _previewView.SurfaceProvider);
            _cameraProvider.UnbindAll();
            _cameraProvider.BindToLifecycle(lifecycleOwner, CameraSelector.DefaultBackCamera, preview);
        }), ContextCompat.GetMainExecutor(context));

        return new PlatformHandle(_previewView.Handle, "Android.CameraX.PreviewView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _cameraProvider?.UnbindAll();
        _cameraProvider?.Dispose();
        _cameraProvider = null;
        _previewView?.Dispose();
        _previewView = null;
        base.DestroyNativeControlCore(control);
    }
}
