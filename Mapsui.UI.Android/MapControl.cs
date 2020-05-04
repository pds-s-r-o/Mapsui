using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.View.Animation;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Mapsui.Geometries;
using Mapsui.Geometries.Utilities;
using Mapsui.Logging;
using Mapsui.UI.Android.Animations;
using Mapsui.UI.Android.GestureListeners;
using SkiaSharp.Views.Android;
using static Android.Views.Animations.Animation;
using static Android.Views.ScaleGestureDetector;
using Math = System.Math;
using Point = Mapsui.Geometries.Point;

namespace Mapsui.UI.Android
{
  public enum SkiaRenderMode
  {
    Accelerated,
    Regular
  }

  public partial class MapControl : ViewGroup, IMapControl
  {
    private View _canvas;
    private double _innerRotation;
    private GestureDetector _gestureDetector;
    private OnLongClickGestureListener _onLongClickListener;
    private RotateGestureDetector _rotateGestureDetector;
    private ScaleGestureDetector _scaleGestureDetector;

    private double _previousAngle;
    private double _previousRadius = 1f;
    private TouchMode _mode = TouchMode.None;
    private Handler _mainLooperHandler;

  
    /// <summary>
    /// Saver for center before last pinch movement
    /// </summary>
    private Point _previousTouch = new Point();
    private SkiaRenderMode _renderMode = SkiaRenderMode.Regular;

    #region Events
    /// <summary>
    /// Called whenever map control is hit by a click 
    /// </summary>
    public new event EventHandler<TappedEventArgs> Click;
    /// <summary>
    /// Called whenever map control is hit by a double click 
    /// </summary>
    public event EventHandler<TappedEventArgs> DoubleClick;
    /// <summary>
    /// Called whenever map control is hit by a long click 
    /// </summary>
    public new event EventHandler<TappedEventArgs> LongClick;

    /// <summary>
    /// Called whenever map control is dragged
    /// </summary>
    public new event EventHandler<DraggedEventArgs> Drag;

    /// <summary>
    /// Called whenever map control is zoomed
    /// </summary>
    public event EventHandler<ZoomedEventArgs> Zoom;

    /// <summary>
    /// Called whenever the pointer hitting map control is up
    /// </summary>
    public event EventHandler<TappedEventArgs> PointerUp;

    /// <summary>
    /// Called whenever the pointer hitting map control is down 
    /// </summary>
    public event EventHandler<TappedEventArgs> PointerDown;

    /// <summary>
    /// Called whenever fling gesture is started;
    /// </summary>
    public event EventHandler<TappedEventArgs> FlingStart;

    /// <summary>
    /// Called whenever fling gesture is finished;
    /// </summary>
    public event EventHandler<TappedEventArgs> FlingEnd;

    /// <summary>
    /// Called whenever fling is being performed.
    /// </summary>
    public event EventHandler<TappedEventArgs> Fling;

    /// <summary>
    /// Length of fling animation
    /// </summary>
    public int? FlingAnimationDuration {get; set;}

    /// <summary>
    /// Fling velocity gets multiplied by this factor.
    /// </summary>
    public double? FlingFactor { get; set; }

    /// <summary>
    /// Fling deccelerate animation will be interpolated using this factor.
    /// </summary>
    public double? FlingDeccelerateFactor { get; set; }

    #endregion


    public MapControl(Context context, IAttributeSet attrs) :
        base(context, attrs)
    {
      Initialize();
    }

    public MapControl(Context context, IAttributeSet attrs, int defStyle) :
        base(context, attrs, defStyle)
    {
      Initialize();
    }

    public void Initialize()
    {
      SetBackgroundColor(Color.Transparent);
      _canvas = StartRegularRenderMode();
      _mainLooperHandler = new Handler(Looper.MainLooper);

      SetViewportSize(); // todo: check if size is available, perhaps we need a load event

      Map = new Map();
      Touch += MapView_Touch;

      _onLongClickListener = new OnLongClickGestureListener();
      _gestureDetector = new GestureDetector(Context, _onLongClickListener, null);
      var rotateListener = new OnRotateGestureListener();
      _rotateGestureDetector = new RotateGestureDetector(Context, rotateListener);
      var scaleListener = new OnScaleGestureListener();
      _scaleGestureDetector = new ScaleGestureDetector(Context, scaleListener) 
        { 
          QuickScaleEnabled = false, 
          StylusScaleEnabled = false 
        };
      scaleListener.Scale += _OnScaled;
      scaleListener.ScaleStart += _OnScaleStart;
      rotateListener.Rotate += _OnRotated;
      rotateListener.RotateEnd += _OnRotationEnd;
      _gestureDetector.DoubleTap += OnDoubleTapped;
      _onLongClickListener.Flinged += OnFlinged;
      _onLongClickListener.LongClick += OnLongTapped;
      _onLongClickListener.SingleClick += OnSingleTapped;
      _gestureDetector.IsLongpressEnabled = true;
    }

    private void CanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
      if (PixelDensity <= 0) return;
      Renderer.Render(e.Surface.Canvas, new Viewport(Viewport), _map.Layers, _map.Widgets, _map.BackColor);
    }

    public SkiaRenderMode RenderMode
    {
      get => _renderMode;
      set
      {
        if (_renderMode == value) return;

        _renderMode = value;
        if (_renderMode == SkiaRenderMode.Accelerated)
        {
          StopRegularRenderMode(_canvas);
          _canvas = StartAcceleratedRenderMode();
        }
        else
        {
          StopAcceleratedRenderMode(_canvas);
          _canvas = StartRegularRenderMode();
        }
        RefreshGraphics();
        OnPropertyChanged();
      }
    }

    private void OnDoubleTapped(object sender, GestureDetector.DoubleTapEventArgs e)
    {
      var position = GetScreenPosition(e.Event, this);
      var positionInPixels = GetScreenPositionInPixels(e.Event, this);
      OnInfo(InvokeInfo(position, position, 2));
      DoubleClick?.Invoke(sender, new TappedEventArgs(position, positionInPixels, 2));
    }

    private void OnSingleTapped(object sender, GestureDetector.SingleTapUpEventArgs e)
    {
      var position = GetScreenPosition(e.Event, this);
      var positionInPixels = GetScreenPositionInPixels(e.Event, this);
      OnInfo(InvokeInfo(position, position, 1));
      Click?.Invoke(sender, new TappedEventArgs(position, positionInPixels, 1));
    }

    private void OnLongTapped(object sender, GestureDetector.LongPressEventArgs e)
    {
      var position = GetScreenPosition(e.Event, this);
      var positionInPixels = GetScreenPositionInPixels(e.Event, this);
      LongClick?.Invoke(sender, new TappedEventArgs(position, positionInPixels, 1));
    }


    private void OnFlinged(object sender, GestureDetector.FlingEventArgs e)
    {
      var timespan = DateTime.Now - _LastZoomTime;
      if (timespan.TotalMilliseconds < 100) return;

      var dx = (e.E1.GetX() - e.E2.GetX());
      var dy = (e.E1.GetY() - e.E2.GetY());

      var dxModified = dx * Math.Abs(e.VelocityX) * (FlingFactor ?? 0.00025);
      var dyModified = dy * Math.Abs(e.VelocityY) * (FlingFactor ?? 0.00025);

      var pixelCoordinate = new Point(e.E1.GetX() - Left, e.E1.GetY() - Top);
      var pixelCoordinate2 = new Point(e.E1.GetX() - Left  - dxModified, e.E1.GetY() - Top - dyModified);

      FlingStart?.Invoke(sender, new TappedEventArgs(pixelCoordinate, 1));

      var anim = new FlingAnimation(this, pixelCoordinate, pixelCoordinate2);
      anim.Duration = FlingAnimationDuration ?? 1000;
      anim.Interpolator = new DecelerateInterpolator((float?)FlingDeccelerateFactor ?? 1f);
      anim.OnFling = (point) =>
      {
        Fling?.Invoke(this, new TappedEventArgs(point, 0));
      };

      anim.PostFling = _FlingEnd;
      StartAnimation(anim);
    }


    private bool _OnRotated(RotateGestureDetector detector)
    {
      if (Map.RotationLock) return false;
      if (_scaleGestureDetector.IsInProgress) return false;

      var rotationDelta = detector.RotationDegreesDelta;

      /*
      if (Viewport.Rotation == 0 && Math.Abs(rotationDelta) < UnSnapRotationDegrees) return true;
      
      ((LimitedViewport)Viewport).SetRotation(Viewport.Rotation - rotationDelta);

      if (Viewport.Rotation % 360 < ReSnapRotationDegrees || Viewport.Rotation % 360 > 360 - ReSnapRotationDegrees)
      {
        ((LimitedViewport)Viewport).SetRotation(0);*/

      ((LimitedViewport)Viewport).SetRotation(Viewport.Rotation - rotationDelta);




      RefreshGraphics();
     // _LastZoomTime = DateTime.Now;
      return true;
    }

    private void _OnRotationEnd(RotateGestureDetector detector)
    {
    }


    private bool _OnScaleStart(ScaleGestureDetector detector)
    {
      PrevFocus = null;
      return true;
    }


    private Point PrevFocus;
    private bool _OnScaled(ScaleGestureDetector detector)
    {
      if (_rotateGestureDetector.IsInProgress()) return false;
      if (PrevFocus == null) PrevFocus = new Point(detector.FocusX / PixelDensity, detector.FocusY / PixelDensity);

      var currFocus = new Point(detector.FocusX / PixelDensity, detector.FocusY / PixelDensity);
      var newRes = Viewport.Resolution / detector.ScaleFactor;

      ((LimitedViewport)Viewport).Transform(
           currFocus,
           PrevFocus, 
           Viewport.Resolution / newRes, 0);


       RefreshGraphics();
       _LastZoomTime = DateTime.Now;
       Zoom?.Invoke(this, new ZoomedEventArgs(Viewport.Center, detector.ScaleFactor < 1 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut));

      PrevFocus = currFocus;
      return true;
    }

    private void _FlingEnd()
    {
      FlingEnd?.Invoke(this, null);
      Refresh();
    }

    protected override void OnSizeChanged(int width, int height, int oldWidth, int oldHeight)
    {
      base.OnSizeChanged(width, height, oldWidth, oldHeight);
      SetViewportSize();
    }

    private void RunOnUIThread(Action action)
    {
      if (SynchronizationContext.Current == null)
        _mainLooperHandler.Post(action);
      else
        action();
    }

    private void CanvasOnPaintSurfaceGL(object sender, SKPaintGLSurfaceEventArgs args)
    {
      args.Surface.Canvas.Scale(PixelDensity, PixelDensity);

      Renderer.Render(args.Surface.Canvas, new Viewport(Viewport), _map.Layers, _map.Widgets, _map.BackColor);
    }


    private DateTime _LastZoomTime;
   

    public void MapView_Touch(object sender, TouchEventArgs args)
    {
      if (_gestureDetector.OnTouchEvent(args.Event))
        return;

      if (args.Event.PointerCount >= 2)
      {
        _scaleGestureDetector.OnTouchEvent(args.Event);
        _rotateGestureDetector.OnTouchEvent(args.Event);
      }


      var touchPoints = GetScreenPositions(args.Event, this);

      switch (args.Event.Action)
      {
        case MotionEventActions.Up:
          Refresh();
          _mode = TouchMode.None;
          PointerUp?.Invoke(this, new TappedEventArgs(GetScreenPosition(args.Event, this), GetScreenPositionInPixels(args.Event, this), 1));
          break;
        case MotionEventActions.Down:
        case MotionEventActions.Pointer1Down:
        case MotionEventActions.Pointer2Down:
        case MotionEventActions.Pointer3Down:
          PointerDown?.Invoke(this, new TappedEventArgs(GetScreenPosition(args.Event, this), GetScreenPositionInPixels(args.Event, this), 1));
          if (touchPoints.Count >= 2)
          {
            (_previousTouch, _previousRadius, _previousAngle) = GetPinchValues(touchPoints);
            _mode = TouchMode.Zooming;
            _innerRotation = Viewport.Rotation;
          }
          else
          {
            _mode = TouchMode.Dragging;
            _previousTouch = touchPoints.First();
          }
          break;
        case MotionEventActions.Pointer1Up:
        case MotionEventActions.Pointer2Up:
        case MotionEventActions.Pointer3Up:
          // Remove the touchPoint that was released from the locations to reset the
          // starting points of the move and rotation
          PointerUp?.Invoke(this, new TappedEventArgs(GetScreenPosition(args.Event, this), GetScreenPositionInPixels(args.Event, this), 1));
          touchPoints.RemoveAt(args.Event.ActionIndex);

          if (touchPoints.Count >= 2)
          {
            (_previousTouch, _previousRadius, _previousAngle) = GetPinchValues(touchPoints);
            _mode = TouchMode.Zooming;
            _innerRotation = Viewport.Rotation;
          }
          else
          {
            _mode = TouchMode.Dragging;
            _previousTouch = touchPoints.First();
          }
          Refresh();
          break;
        case MotionEventActions.Move:
          switch (_mode)
          {
            case TouchMode.Dragging:
              {
                if (touchPoints.Count != 1)
                  return;

                var touch = touchPoints.First();

                Drag?.Invoke(this, new DraggedEventArgs(_previousTouch, touch));

                if (_previousTouch != null && !_previousTouch.IsEmpty())
                {

                  
                  _viewport.Transform(touch, _previousTouch);
                  RefreshGraphics();
                }
                _previousTouch = touch;

              }
              break;
            case TouchMode.Zooming:
              /*{
                if (touchPoints.Count < 2 || _rotateGestureDetector.IsInProgress())
                  return;

                var (previousTouch, previousRadius, previousAngle) = (_previousTouch, _previousRadius, _previousAngle);
                var (touch, radius, angle) = GetPinchValues(touchPoints);
                Zoom?.Invoke(this, new ZoomedEventArgs(touch, ZoomDirection.ZoomIn));

                _viewport.Transform(touch, previousTouch, radius / previousRadius);
                RefreshGraphics();

                (_previousTouch, _previousRadius, _previousAngle) = (touch, radius, angle);


                _LastZoomTime = DateTime.Now;
              }*/
              break;
          }
          break;
      }
    }

    /// <summary>
    /// Gets the screen position in device independent units relative to the MapControl.
    /// </summary>
    /// <param name="motionEvent"></param>
    /// <param name="view"></param>
    /// <returns></returns>
    private List<Point> GetScreenPositions(MotionEvent motionEvent, View view)
    {
      var result = new List<Point>();
      for (var i = 0; i < motionEvent.PointerCount; i++)
      {
        var pixelCoordinate = new Point(motionEvent.GetX(i) - view.Left, motionEvent.GetY(i) - view.Top);
        result.Add(pixelCoordinate.ToDeviceIndependentUnits(PixelDensity));
      }
      return result;
    }

    /// <summary>
    /// Gets the screen position in device independent units relative to the MapControl.
    /// </summary>
    /// <param name="motionEvent"></param>
    /// <param name="view"></param>
    /// <returns></returns>
    private Point GetScreenPosition(MotionEvent motionEvent, View view)
    {
      return GetScreenPositionInPixels(motionEvent, view).ToDeviceIndependentUnits(PixelDensity);
    }

    /// <summary>
    /// Gets the screen position in pixels relative to the MapControl.
    /// </summary>
    /// <param name="motionEvent"></param>
    /// <param name="view"></param>
    /// <returns></returns>
    private static Point GetScreenPositionInPixels(MotionEvent motionEvent, View view)
    {
      return new PointF(motionEvent.GetX(0) - view.Left, motionEvent.GetY(0) - view.Top).ToMapsui();
    }

    public void RefreshGraphics()
    {
      RunOnUIThread(RefreshGraphicsWithTryCatch);
    }

    private void RefreshGraphicsWithTryCatch()
    {
      try
      {
        // Both Invalidate and _canvas.Invalidate are necessary in different scenarios.
        Invalidate();
        _canvas?.Invalidate();
      }
      catch (ObjectDisposedException e)
      {
        // See issue: https://github.com/Mapsui/Mapsui/issues/433
        // What seems to be happening. The Activity is Disposed. Appently it's children get Disposed
        // explicitly by something in Xamarin. During this Dispose the MessageCenter, which is itself
        // not disposed gets another notification to call RefreshGraphics.
        Logger.Log(LogLevel.Warning, "This can happen when the parent Activity is disposing.", e);
      }
    }

    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
      SetBounds(_canvas, l, t, r, b);
    }

    private static void SetBounds(View view, int l, int t, int r, int b)
    {
      view.Top = t;
      view.Bottom = b;
      view.Left = l;
      view.Right = r;
    }

    public void OpenBrowser(string url)
    {
      global::Android.Net.Uri uri = global::Android.Net.Uri.Parse(url);
      Intent intent = new Intent(Intent.ActionView);
      intent.SetData(uri);

      Intent chooser = Intent.CreateChooser(intent, "Open with");

      Context.StartActivity(chooser);
    }

    public new void Dispose()
    {
      Unsubscribe();
      base.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
      Unsubscribe();
      base.Dispose(disposing);
    }

    private static (Point centre, double radius, double angle) GetPinchValues(List<Point> locations)
    {
      if (locations.Count < 2)
        throw new ArgumentException();

      double centerX = 0;
      double centerY = 0;

      foreach (var location in locations)
      {
        centerX += location.X;
        centerY += location.Y;
      }

      centerX = centerX / locations.Count;
      centerY = centerY / locations.Count;

      var radius = Algorithms.Distance(centerX, centerY, locations[0].X, locations[0].Y);

      var angle = Math.Atan2(locations[1].Y - locations[0].Y, locations[1].X - locations[0].X) * 180.0 / Math.PI;

      return (new Point(centerX, centerY), radius, angle);
    }

    private float ViewportWidth => ToDeviceIndependentUnits(Width);
    private float ViewportHeight => ToDeviceIndependentUnits(Height);

    /// <summary>
    /// In native Android touch positions are in pixels whereas the canvas needs
    /// to be drawn in device independent units (otherwise labels on raster tiles will be unreadable
    /// and symbols will be too small). This method converts pixels to device independent units.
    /// </summary>
    /// <returns>The pixels given as input translated to device independent units.</returns>
    private float ToDeviceIndependentUnits(float pixelCoordinate)
    {
      return pixelCoordinate / PixelDensity;
    }

    private View StartRegularRenderMode()
    {
      var canvas = new SKCanvasView(Context) { IgnorePixelScaling = true };
      canvas.PaintSurface += CanvasOnPaintSurface;
      AddView(canvas);
      return canvas;
    }

    private void StopRegularRenderMode(View canvas)
    {
      if (canvas is SKCanvasView canvasView)
      {
        canvasView.PaintSurface -= CanvasOnPaintSurface;
        RemoveView(canvasView);
        // Let's not dispose. The Paint callback might still be busy.
      }
    }

    private View StartAcceleratedRenderMode()
    {
      var canvas = new SKGLSurfaceView(Context);
      canvas.PaintSurface += CanvasOnPaintSurfaceGL;
      AddView(canvas);
      return canvas;
    }

    private void StopAcceleratedRenderMode(View canvas)
    {
      if (canvas is SKGLSurfaceView surfaceView)
      {
        surfaceView.PaintSurface -= CanvasOnPaintSurfaceGL;
        RemoveView(surfaceView);
        // Let's not dispose. The Paint callback might still be busy.
      }
    }

    private float GetPixelDensity()
    {
      return Resources.DisplayMetrics.Density;
    }
  }
}