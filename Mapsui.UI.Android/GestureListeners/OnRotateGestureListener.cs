using Android.Views;
using static Android.Views.RotateGestureDetector;

namespace Mapsui.UI.Android.GestureListeners
{
  public class OnRotateGestureListener : IOnRotateGestureListener
  {
    public delegate bool RotateEvent(RotateGestureDetector detector);
    public delegate bool RotateBeginEvent(RotateGestureDetector detector);
    public delegate void RotateEndEvent(RotateGestureDetector detector);

    public RotateEvent Rotate { get; set; }
    public RotateBeginEvent RotateBegin { get; set; }
    public RotateEndEvent RotateEnd { get; set; }

    public bool OnRotate(RotateGestureDetector detector)
    {
      return Rotate.Invoke(detector);
    }

    public bool OnRotateBegin(RotateGestureDetector detector)
    {
      if (RotateBegin == null) return false;
      return RotateBegin.Invoke(detector);
    }

    public void OnRotateEnd(RotateGestureDetector detector)
    {
    
      RotateEnd?.Invoke(detector);
    }
  }
}