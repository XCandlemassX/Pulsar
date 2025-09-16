using SharpDX.DXGI;

namespace Pulsar.Client.Helper.ScreenStuff.DesktopDuplication
{
    internal class PointerInfo
    {
        public byte[] PtrShapeBuffer;
        public OutputDuplicatePointerShapeInformation ShapeInfo;
        public SharpDX.Point Position;
        public bool Visible;
        public int BufferSize;
        public int WhoUpdatedPositionLast;
        public long LastTimeStamp;
    }
}
