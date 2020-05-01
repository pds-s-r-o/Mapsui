using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Mapsui.Geometries;

namespace Mapsui.UI.Android.Animations
{
  public class FlingAnimation : Animation
  {
    MapControl _MapControl;
    LimitedViewport _Viewport;
    Point _StartPos;
    Point _EndPos;
    Point _InterPos;

    public Action<Point> OnFling;

    public FlingAnimation(MapControl mapControl, Point startPosition, Point endPosition)
    {
      _MapControl = mapControl;
      _Viewport = (LimitedViewport)mapControl.Viewport;
      _StartPos = startPosition;
      _EndPos = endPosition;
    }

    protected override void ApplyTransformation(float interpolatedTime, Transformation t)
    {
      var dx = _EndPos.X - _StartPos.X;
      var dy = _EndPos.Y - _StartPos.Y;

      _InterPos = new Point(_StartPos.X + (dx * interpolatedTime), _StartPos.Y + (dy * interpolatedTime));
      _Viewport.Transform(_InterPos, _StartPos);
      _StartPos.X = _InterPos.X;
      _StartPos.Y = _InterPos.Y;
      _MapControl.RefreshGraphics();
      OnFling?.Invoke(_InterPos);
      base.ApplyTransformation(interpolatedTime, t);
    }
  }
}