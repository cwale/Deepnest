namespace DeepNestRhino.Geometry
{
    /// <summary>
    /// Axis-aligned bounding rectangle.
    /// Maps to the bounds object returned by GeometryUtil.getPolygonBounds().
    /// </summary>
    public struct RectangleBounds
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public RectangleBounds(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
