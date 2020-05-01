using Android.Views;

namespace Mapsui.UI.Android.GestureListeners
{
  public class OnLongClickGestureListener : GestureDetector.SimpleOnGestureListener
  {
    public delegate void LongPress(object sender, GestureDetector.LongPressEventArgs args);
    public delegate void SinglePress(object sender, GestureDetector.SingleTapUpEventArgs args);
    public delegate void Fling(object sender, GestureDetector.FlingEventArgs args);

    public LongPress LongClick { get; set; }
    public SinglePress SingleClick { get; set; }
    public Fling Flinged { get; set; }

    public override void OnLongPress(MotionEvent e)
    {
      base.OnLongPress(e);
      LongClick(this, new GestureDetector.LongPressEventArgs(e));
    }

    public override bool OnSingleTapUp(MotionEvent e)
    {
      SingleClick(this, new GestureDetector.SingleTapUpEventArgs(false, e));
      return base.OnSingleTapUp(e);
    }

    public override bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
    {
      Flinged(this, new GestureDetector.FlingEventArgs(false, e1, e2, velocityX, velocityY));
      return base.OnFling(e1, e2, velocityX, velocityY);
    }
  }
}