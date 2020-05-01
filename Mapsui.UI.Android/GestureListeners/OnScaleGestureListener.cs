using Android.Views;
using static Android.Views.ScaleGestureDetector;

namespace Mapsui.UI.Android.GestureListeners
{
  public class OnScaleGestureListener : SimpleOnScaleGestureListener
  {
    public delegate bool ScaleEvent(ScaleGestureDetector detector);
    public delegate bool ScaleStartEvent(ScaleGestureDetector detector);
    public delegate void ScaleEndEvent(ScaleGestureDetector detector);

    public ScaleEvent Scale { get; set; }
    public ScaleStartEvent ScaleStart { get; set; }
    public ScaleEndEvent ScaleEnd {get; set;}
    public override bool OnScale(ScaleGestureDetector detector)
    {
      if (Scale == null) return base.OnScale(detector);
      else return Scale.Invoke(detector);

    }

    public override bool OnScaleBegin(ScaleGestureDetector detector)
    {
      ScaleStart?.Invoke(detector);
      return base.OnScaleBegin(detector);
    }

    public override void OnScaleEnd(ScaleGestureDetector detector)
    {
      ScaleEnd?.Invoke(detector);
      base.OnScaleEnd(detector);
    }
  }
}