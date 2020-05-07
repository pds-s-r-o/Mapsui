namespace Mapsui.UI.EventArgs
{
	public class RotatedEventArgs : EventArgs
	{
		public float RotationOffset { get; set; }
		public RotatedEventArgs(float offset)
		{
			RotationOffset = offset;
		}
	}
}
