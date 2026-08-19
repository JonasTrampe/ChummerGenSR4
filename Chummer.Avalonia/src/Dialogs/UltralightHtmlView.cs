using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using UltralightNet;

namespace Chummer.NewUI.Dialogs;

/// <summary>Renders static HTML to a bitmap via Ultralight (a non-Chromium, non-WebKit HTML/CSS
/// engine that renders off-screen to a pixel buffer rather than a native OS window) and displays
/// it as a plain Avalonia Image. Chosen specifically because Avalonia's own WebView control
/// (WebKitGTK-backed on Linux) failed to render at all when embedded as a child control - Mesa/
/// NVIDIA GBM buffer sharing between GTK's native widget and Avalonia's own Skia-rendered canvas
/// is the piece that broke, and Ultralight sidesteps that whole problem since there's no native
/// widget to embed, only a bitmap this control blits itself.
///
/// Uses the single process-wide <see cref="Program.UltralightRenderer"/> - Ultralight's engine is
/// a singleton internally (thread pools, ICU data, font caches are set up once), so only Views
/// are created/disposed per render, never Renderers.
///
/// Read-only preview only (no scripting/interactivity needed for a character sheet) - scrolling
/// is handled by wrapping this in a normal Avalonia ScrollViewer, not by forwarding input into
/// Ultralight.</summary>
public sealed class UltralightHtmlView : Image
{
    private const uint ProbeHeight = 50;
    private const int MaxLoadWaitAttempts = 400;

    public void LoadHtml(string strHtml, uint uiWidth)
    {
        Renderer renderer = NewUI.Program.UltralightRenderer;

        // Pass 1: a small probe view just to measure the page's real content height - rendering
        // directly into an oversized view produces a tiling/ghosting artifact on this engine's CPU
        // (non-accelerated) render path when the view is much taller than its actual content.
        uint uiMeasuredHeight = ProbeHeight;
        using (View probeView = CreateView(renderer, uiWidth, ProbeHeight))
        {
            probeView.LoadHtml(strHtml);
            WaitForLoad(renderer, probeView);
            string strHeight = probeView.EvaluateScript("document.body ? document.body.scrollHeight : " + ProbeHeight, out _);
            if (uint.TryParse(strHeight, out uint uiParsedHeight) && uiParsedHeight > 0)
                uiMeasuredHeight = uiParsedHeight;
        }

        // Pass 2: a fresh view at the exact measured height, matching the probe-then-resize
        // pattern that avoided the ghosting artifact.
        using View sizedView = CreateView(renderer, uiWidth, uiMeasuredHeight);
        sizedView.LoadHtml(strHtml);
        WaitForLoad(renderer, sizedView);
        renderer.Render();
        RefreshBitmapFromSurface(sizedView);
    }

    private static View CreateView(Renderer renderer, uint uiWidth, uint uiHeight) =>
        renderer.CreateView(uiWidth, uiHeight,
            new ViewConfig { IsAccelerated = false, EnableJavaScript = true },
            renderer.DefaultSession, dispose: true);

    private static void WaitForLoad(Renderer renderer, View view)
    {
        for (int intAttempt = 0; intAttempt < MaxLoadWaitAttempts && view.IsLoading; intAttempt++)
        {
            renderer.Update();
            Thread.Sleep(5);
        }
        renderer.Update();
    }

    private void RefreshBitmapFromSurface(View view)
    {
        if (view.Surface is not { } surface)
            return;

        UlBitmap bitmap = surface.Bitmap;
        unsafe
        {
            byte* pixels = bitmap.LockPixels();
            try
            {
                var wb = new WriteableBitmap(new PixelSize((int)bitmap.Width, (int)bitmap.Height),
                    new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var locked = wb.Lock())
                {
                    // Copy row-by-row rather than one bulk copy - Ultralight's RowBytes and
                    // Avalonia's own computed stride aren't guaranteed to match (row padding
                    // rules can differ), so a single contiguous copy could scramble rows.
                    int intCopyBytesPerRow = Math.Min((int)bitmap.RowBytes, locked.RowBytes);
                    for (int intRow = 0; intRow < bitmap.Height; intRow++)
                    {
                        Buffer.MemoryCopy(
                            pixels + (long)intRow * bitmap.RowBytes,
                            (byte*)locked.Address + intRow * locked.RowBytes,
                            locked.RowBytes, intCopyBytesPerRow);
                    }
                }
                Source = wb;
                Width = bitmap.Width;
                Height = bitmap.Height;
            }
            finally
            {
                bitmap.UnlockPixels();
            }
        }
    }
}
