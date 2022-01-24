using MathSupport;
using OpenglSupport;
using OpenTK;
using System;

namespace _096puzzle
{
  public class Cube : DefaultRenderObject
  {
    /// <summary>
    /// Cube center.
    /// </summary>
    public Vector3d startPosition;

    /// <summary>
    /// Cube position after rotation.
    /// </summary>
    public Vector3d actualPosition;

    /// <summary>
    /// Object to world transform matrix.
    /// </summary>
    public Matrix4d objectMatrix = Matrix4d.Identity;

    /// <summary>
    /// Cobe size.
    /// </summary>
    public double size;

    /// <summary>
    /// Last simulated time in seconds.
    /// </summary>
    public double simTime;

    /// <summary>
    /// Revolution in radians per second.
    /// </summary>
    public double speed = 0.25;

    /// <summary>
    /// Current rotation axis.
    /// </summary>
    public Vector3d axis = new Vector3d(0,0,1); //Co je tohle za past?????

    /// <summary>
    /// Angle to rotate (0.0 if no rotation is necessary).
    /// angleLeft += 'sign' * speed * dt;
    /// </summary>
    public double angleLeft = 0;

    /// <summary>
    /// Check the status of cube. If "True", rotate.
    /// </summary>
    public bool shouldAnimate = false;

    public bool shouldRotate = true;

    /// <summary>
    /// Represents quaters of round in radians.
    /// </summary>
    private  double [] radianAngles = new double[] {0, 1.5707, 3.1415, 4.7123, 6.2831};

    /// <summary>
    /// After every rotation set the final angle.
    /// </summary>
    public double lastSettedXRotation = 0;

    /// <summary>
    /// After every rotation set the final angle.
    /// </summary>
    public double lastSettedYRotation = 0;

    /// <summary>
    /// After every rotation set the final angle.
    /// </summary>
    public double lastSettedZRotation = 0;

    /// <summary>
    /// Current type of rotation
    /// </summary>
    public RotationType rotationType;

    /// <summary>
    /// Unique id for each cube.
    /// </summary>
    public uint id;

    /// <summary>
    /// Check if cube have begun rotate.
    /// </summary>
    public bool Visited = false;

    /// <summary>
    /// Scpecial animation.
    /// </summary>
    private int scaleIteration = 0;

    public bool scale = false;

    private bool shouldScale = true;

    private Vector3d scaleBackupStartPoint = new Vector3d(Double.MinValue, Double.MinValue, Double.MinValue);

    /// <summary>
    /// Color settings.
    /// </summary>
    Vector3 colorFront = Vector3.UnitX;
    Vector3 colorRight = Vector3.UnitZ;
    Vector3 colorBack = Vector3.UnitX + 0.5f * Vector3.UnitY;
    Vector3 colorLeft = Vector3.UnitY;
    Vector3 colorTop = new Vector3(1.0f, 1.0f, 1.0f);
    Vector3 colorBottom = Vector3.UnitX + Vector3.UnitY;

    public Cube (Vector3d pos, double siz, double time, uint id, Vector3 colorFront, Vector3 colorRight, Vector3 colorBack, Vector3 colorLeft, Vector3 colorTop, Vector3 colorBottom)
    {
      startPosition = pos;
      actualPosition = pos;
      size = siz;
      this.id = id;

      this.colorFront = colorFront;
      this.colorRight = colorRight;
      this.colorBack = colorBack;
      this.colorLeft = colorLeft;
      this.colorTop = colorTop;
      this.colorBottom = colorBottom;

      Reset(time);
      fillCache();
    }

    public void Reset (double time)
    {
      simTime = time;
      angleLeft = 0.0;
    }


    public double GetRoundQuatersInRadians (double degrees, bool negative = false)
    {
      int sign = 1;
      if (negative)
        sign = -1;
      degrees = Math.Abs(degrees);

      if (degrees == 0)
        return sign * radianAngles[0];
      else if (degrees == 90)
        return sign * radianAngles[1];
      else if (degrees == 180)
        return sign * radianAngles[2];
      else if (degrees == 270)
        return sign * radianAngles[3];
      else
        return sign * radianAngles[4];
    }

    public bool CheckRotation ()
    {
      double newSettedRotation;
      if (rotationType == RotationType.ZPositive || rotationType == RotationType.ZNegative)
      {
        DecreaseScaleDuringRotation (1, 0.977, 0.977);
        if (CheckAngle(lastSettedZRotation, rotationType, out newSettedRotation))
        {
          lastSettedZRotation = newSettedRotation;
          RoundMatrix(rotationType, lastSettedZRotation);
          return true;
        }
      }
      else if (rotationType == RotationType.YPositive || rotationType == RotationType.YNegative)
      {
        DecreaseScaleDuringRotation(1, 0.977, 0.977);
        if (CheckAngle(lastSettedYRotation, rotationType, out newSettedRotation))
        {
          lastSettedYRotation = newSettedRotation;
          RoundMatrix(rotationType, lastSettedYRotation);
          return true;
        }
      }
      else
      {
        DecreaseScaleDuringRotation(0.977, 1, 0.977);
        if (CheckAngle(lastSettedXRotation, rotationType, out newSettedRotation))
        {
          lastSettedXRotation = newSettedRotation;
          RoundMatrix(rotationType, lastSettedXRotation);
          return true;
        }
      }
      return false;
    }

    public void RoundMatrix (RotationType rotationType, double lastSettedRotation)
    {
      objectMatrix.Row0.W = Math.Round(objectMatrix.Row0.W);
      objectMatrix.Row0.X = Math.Round(objectMatrix.Row0.X);
      objectMatrix.Row0.Y = Math.Round(objectMatrix.Row0.Y);
      objectMatrix.Row0.Z = Math.Round(objectMatrix.Row0.Z);

      objectMatrix.Row1.W = Math.Round(objectMatrix.Row1.W);
      objectMatrix.Row1.X = Math.Round(objectMatrix.Row1.X);
      objectMatrix.Row1.Y = Math.Round(objectMatrix.Row1.Y);
      objectMatrix.Row1.Z = Math.Round(objectMatrix.Row1.Z);

      objectMatrix.Row2.W = Math.Round(objectMatrix.Row2.W);
      objectMatrix.Row2.X = Math.Round(objectMatrix.Row2.X);
      objectMatrix.Row2.Y = Math.Round(objectMatrix.Row2.Y);
      objectMatrix.Row2.Z = Math.Round(objectMatrix.Row2.Z);

      objectMatrix.Row3.W = Math.Round(objectMatrix.Row3.W);
      objectMatrix.Row3.X = Math.Round(objectMatrix.Row3.X);
      objectMatrix.Row3.Y = Math.Round(objectMatrix.Row3.Y);
      objectMatrix.Row3.Z = Math.Round(objectMatrix.Row3.Z);

      if (lastSettedRotation == 360)
      {
        lastSettedRotation = 0;
      }
      angleLeft = 0;
      Visited = false;
    }

    public bool CheckAngle (double lastSettedRotation, RotationType rotationType, out double newSettedRotation)
    {
      if (angleLeft > 0)
      {
        if (angleLeft >= GetRoundQuatersInRadians(90) && angleLeft != 0)
        {
          newSettedRotation = lastSettedRotation + 90;
          angleLeft = 0;
          return true;
        }
      }
      else
      {
        if (angleLeft <= GetRoundQuatersInRadians(90, true) && angleLeft != 0)
        {
          newSettedRotation = lastSettedRotation - 90;
          angleLeft = 0;
          return true;
        }
      }
      newSettedRotation = double.NaN;
      return false;
    }

    private void DecreaseScaleDuringRotation (double scaleX, double scaleY, double scaleZ)
    {
      if (scaleIteration > 0)
      {
        startPosition.X *= scaleX;
        startPosition.Y *= scaleY;
        startPosition.Z *= scaleZ;
        scaleIteration -= 1;
      }
    }

    public bool DecreaseScaleAfterRotation ()
    {
      bool finished = true;

      if (Math.Abs(scaleBackupStartPoint.X - startPosition.X) > 0.2)
      {
        if (scaleBackupStartPoint.X > startPosition.X)
          startPosition.X += 0.2;
        else if (scaleBackupStartPoint.X < startPosition.X)
          startPosition.X -= 0.2;
        finished = false;
      }

      if (Math.Abs(scaleBackupStartPoint.Y - startPosition.Y) > 0.2)
      {
        if (scaleBackupStartPoint.Y > startPosition.Y)
          startPosition.Y += 0.2;
        else if (scaleBackupStartPoint.Y < startPosition.Y)
          startPosition.Y -= 0.2;
        finished = false;
      }

      if (Math.Abs(scaleBackupStartPoint.Z - startPosition.Z) > 0.2)
      {
        if (scaleBackupStartPoint.Z > startPosition.Z)
          startPosition.Z += 0.2;
        else if (scaleBackupStartPoint.Z < startPosition.Z)
          startPosition.Z -= 0.2;
        finished = false;
      }
      return true;
    }

    private void ClearScale ()
    {
      if (scaleBackupStartPoint.X != Double.MinValue)
      {
        startPosition.X = scaleBackupStartPoint.X;
        startPosition.Y = scaleBackupStartPoint.Y;
        startPosition.Z = scaleBackupStartPoint.Z;

        scaleBackupStartPoint = new Vector3d(Double.MinValue, Double.MinValue, Double.MinValue);
        scaleIteration = 0;
      }
    }
    private void ScaleStartPosition ()
    {
      if (scaleBackupStartPoint.X == Double.MinValue)
      {
        scaleBackupStartPoint.X = startPosition.X;
        scaleBackupStartPoint.Y = startPosition.Y;
        scaleBackupStartPoint.Z = startPosition.Z;
      }

      if (rotationType == RotationType.ZPositive || rotationType == RotationType.ZNegative)
      {
        startPosition.X *= 1.02;
        startPosition.Y *= 1.02;
        startPosition.Z *= 1.00;
      }
      else if (rotationType == RotationType.YPositive || rotationType == RotationType.YNegative)
      {
        startPosition.X *= 1.0;
        startPosition.Y *= 1.02;
        startPosition.Z *= 1.02;
      }
      else
      {
        startPosition.X *= 1.02;
        startPosition.Y *= 1.0;
        startPosition.Z *= 1.02;
      }
      scaleIteration++;
    }

    private void Rotate ()
    {
      var rotationAngle = 0.0;
      if ((int)rotationType % 2 == 0)
        rotationAngle = 0.04;
      else
        rotationAngle = -0.04;

      Matrix4d rotationM;
      if (axis.X != 0)
        rotationM = Matrix4d.RotateX(rotationAngle);
      else if (axis.Y != 0)
        rotationM = Matrix4d.RotateY(rotationAngle);
      else
        rotationM = Matrix4d.RotateZ(rotationAngle);

      angleLeft += rotationAngle;
      objectMatrix *= rotationM;
    }

    /// <summary>
    /// Simulate object to the given time.
    /// </summary>
    /// <param name="time">Required target time.</param>
    /// <param name="puz">Puzzle context.</param>
    /// <returns>False in case of expiry.</returns>
    public bool Simulate (double time, Puzzle puz)
    {
      if (time <= simTime)
        return true;

      if (shouldAnimate)
      {
        Visited = true;
        if (scale && scaleIteration != 45 && shouldScale)
        {
          ScaleStartPosition();
        }
        else if (shouldRotate)
        {
          shouldScale = false;
          Rotate();
          if (CheckRotation())
          {
            if (scale)
            {
              shouldRotate = false;
            }
            else
            {
              shouldAnimate = false;
              shouldScale = true;
              actualPosition = Vector3d.Transform(startPosition, objectMatrix);
            }
          }
        }
        else
        {
          if (DecreaseScaleAfterRotation())
          {
            if (time > simTime)
            {
              shouldRotate = true;
              shouldScale = true;
              ClearScale();
              actualPosition = Vector3d.Transform(startPosition, objectMatrix);
              Visited = false;
              shouldAnimate = false;
              scale = false;
            }
          }
        }
      }
      simTime = time;
      return true;
    }

    /// <summary>
    /// Pointing to the cube.
    /// </summary>
    /// <param name="p0">Ray origin.</param>
    /// <param name="p1">Ray direction.</param>
    /// <param name="action">Do interaction?</param>
    /// <returns>Intersection parameter or double.PositiveInfinity if no intersection exists.</returns>
    public double Intersect (ref Vector3d p0, ref Vector3d p1, bool action = false)
    {
      Vector3d A, B, C;
      Vector2d uv;
      double nearest = double.PositiveInfinity;
      int inearest = 0;
      uint ix;

      for (int i = 0; i + 2 < ind.Length; i += 3)
      {
        ix = ind[i] * 3;
        A = Vector3d.TransformPosition(new Vector3d(vert[ix], vert[ix + 1], vert[ix + 2]), objectMatrix);
        ix = ind[i + 1] * 3;
        B = Vector3d.TransformPosition(new Vector3d(vert[ix], vert[ix + 1], vert[ix + 2]), objectMatrix);
        ix = ind[i + 2] * 3;
        C = Vector3d.TransformPosition(new Vector3d(vert[ix], vert[ix + 1], vert[ix + 2]), objectMatrix);
        double curr = Geometry.RayTriangleIntersection( ref p0, ref p1, ref A, ref B, ref C, out uv );
        if (!double.IsInfinity(curr) &&
             curr < nearest)
        {
          nearest = curr;
          inearest = i;
        }
      }

      return nearest;
    }

    //--- rendering ---

    /// <summary>
    /// Vertex array cache. Object coordinates.
    /// </summary>
    float[] vert = null;

    /// <summary>
    /// Index buffer cache.
    /// </summary>
    uint[] ind = null;

    unsafe void fillCache ()
    {
      uint ori = 0;
      int stride;
      float* ptr = null;
      int vsize = TriangleVertices( ref ptr, ref ori, out stride, false, false, false, false ) / sizeof( float );
      vert = new float[vsize];
      fixed (float* p = vert)
      {
        float* pp = p;
        TriangleVertices(ref pp, ref ori, out stride, false, false, false, false);
      }
      uint* iptr = null;
      int isize = TriangleIndices( ref iptr, 0 ) / sizeof( uint );
      ind = new uint[isize];
      fixed (uint* ip = ind)
      {
        uint* ipp = ip;
        TriangleIndices(ref ipp, 0);
      }
    }

    public override uint Triangles
    {
      get
      {
        return 12;
      }
    }

    public override uint TriVertices
    {
      get
      {
        return 24;
      }
    }

    unsafe void FaceVertices (ref float* ptr, ref Vector3d corner, ref Vector3d side1, ref Vector3d side2, ref Vector3d n, ref Vector3 color,
                               bool txt, bool col, bool normal, bool ptsize)
    {
      // Upper left.
      if (txt)
        Fill(ref ptr, 0.0f, 0.0f);
      if (col)
        Fill(ref ptr, ref color);
      if (normal)
        Fill(ref ptr, ref n);
      if (ptsize)
        *ptr++ = 1.0f;
      Fill(ref ptr, Vector3d.TransformPosition(corner, objectMatrix));

      // Upper right.
      if (txt)
        Fill(ref ptr, 1.0f, 0.0f);
      if (col)
        Fill(ref ptr, ref color);
      if (normal)
        Fill(ref ptr, ref n);
      if (ptsize)
        *ptr++ = 1.0f;
      Fill(ref ptr, Vector3d.TransformPosition(corner + side1, objectMatrix));

      // Lower left.
      if (txt)
        Fill(ref ptr, 0.0f, 1.0f);
      if (col)
        Fill(ref ptr, ref color);
      if (normal)
        Fill(ref ptr, ref n);
      if (ptsize)
        *ptr++ = 1.0f;
      Fill(ref ptr, Vector3d.TransformPosition(corner + side2, objectMatrix));

      // Lower right.
      if (txt)
        Fill(ref ptr, 1.0f, 1.0f);
      if (col)
        Fill(ref ptr, ref color);
      if (normal)
        Fill(ref ptr, ref n);
      if (ptsize)
        *ptr++ = 1.0f;
      Fill(ref ptr, Vector3d.TransformPosition(corner + side1 + side2, objectMatrix));
    }

    /// <summary>
    /// Triangles: returns vertex-array size (if ptr is null) or fills vertex array.
    /// </summary>
    /// <returns>Data size of the vertex-set (in bytes).</returns>
    public override unsafe int TriangleVertices (ref float* ptr, ref uint origin, out int stride, bool txt, bool col, bool normal, bool ptsize)
    {
      int total = base.TriangleVertices( ref ptr, ref origin, out stride, txt, col, normal, ptsize );
      if (ptr == null)
        return total;

      Vector3d corner, n, side1, side2;
      double s2 = size * 0.5;
      Vector3 color;

      // 1. front
      corner.X = startPosition.X - s2;
      corner.Y = startPosition.Y + s2;
      corner.Z = startPosition.Z + s2;
      side1 = Vector3d.UnitX * size;
      side2 = Vector3d.UnitY * -size;
      n = Vector3d.UnitZ;
      color = colorFront; // red
      FaceVertices(ref ptr, ref corner, ref side1, ref side2, ref n, ref color, txt, col, normal, ptsize);

      // 2. right
      corner += side1;
      side1 = Vector3d.UnitZ * -size;
      n = Vector3d.UnitX;
      color = colorRight; // blue
      FaceVertices(ref ptr, ref corner, ref side1, ref side2, ref n, ref color, txt, col, normal, ptsize);

      // 3. back
      corner += side1;
      side1 = Vector3d.UnitX * -size;
      n = -Vector3d.UnitZ;
      color = colorBack; // orange
      FaceVertices(ref ptr, ref corner, ref side1, ref side2, ref n, ref color, txt, col, normal, ptsize);

      // 4. left
      corner += side1;
      side1 = Vector3d.UnitZ * size;
      n = -Vector3d.UnitX;
      color = colorLeft; // green
      FaceVertices(ref ptr, ref corner, ref side1, ref side2, ref n, ref color, txt, col, normal, ptsize);

      // 5. top
      side1 = Vector3d.UnitX * size;
      side2 = Vector3d.UnitZ * size;
      n = Vector3d.UnitY;
      color = colorTop; // white
      FaceVertices(ref ptr, ref corner, ref side1, ref side2, ref n, ref color, txt, col, normal, ptsize);

      // 6. bottom
      corner.X = startPosition.X - s2;
      corner.Y = startPosition.Y - s2;
      corner.Z = startPosition.Z + s2;
      side1 = Vector3d.UnitX * size;
      side2 = Vector3d.UnitZ * -size;
      n = -Vector3d.UnitY;
      color = colorBottom; // yellow
      FaceVertices(ref ptr, ref corner, ref side1, ref side2, ref n, ref color, txt, col, normal, ptsize);

      return total;
    }

    uint origin0 = 0;

    /// <summary>
    /// Triangles: returns index-array size (if ptr is null) or fills index array.
    /// </summary>
    /// <returns>Data size of the index-set (in bytes).</returns>
    public override unsafe int TriangleIndices (ref uint* ptr, uint origin)
    {
      if (ptr != null)
      {
        origin0 = origin;

        for (int i = 0; i++ < 6; origin += 4)
        {
          *ptr++ = origin;
          *ptr++ = origin + 2;
          *ptr++ = origin + 1;

          *ptr++ = origin + 2;
          *ptr++ = origin + 3;
          *ptr++ = origin + 1;
        }
      }

      return 36 * sizeof(uint);
    }
  }
}
