using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MathSupport;
using OpenglSupport;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Utilities;

namespace _096puzzle
{
  public enum SelectionType { X, Y, Z }
  public enum RotationType
  {
    ZPositive,
    ZNegative,
    YPositive,
    YNegative,
    XPositive,
    XNegative
  } //Dont change the order!!!

  public partial class Form1
  {
    /// <summary>
    /// Form-data initialization.
    /// </summary>
    static void InitParams (out string name, out string param, out string tooltip, out Vector3 center, out float diameter)
    {
      // {{

      name = "Milan Kotva";
      param = "speed=1.0,slow=0.25,size=5,specialAnimation=false,shuffle=false";
      tooltip = "speed = <float>, slow = <float> , size = <int>, specialAnimation = <bool>, shuffle = <bool>";
      center = new Vector3(0.0f, 0.0f, 0.0f);
      diameter = 4.0f;

      // }}
    }

    uint[] VBOid = null;  // vertex array VBO (colors, normals, coords), index array VBO
    int[] VBOlen = null;  // currently allocated lengths of VBOs

    /// <summary>
    /// Simulation object.
    /// </summary>
    Puzzle puz = null;

    long lastFpsTime = 0L;
    int frameCounter = 0;
    long primitiveCounter = 0L;
    double lastFps = 0.0;
    double lastPps = 0.0;
    Cube minCube = null;

    Vector3d intersectVector1;
    Vector3d intersectVector2;

    double mouseKeyPressCoordinatesX = double.NaN;
    double mouseKeyPressCoordinatesY = double.NaN;

    /// <summary>
    /// Function called whenever the main application is idle..
    /// </summary>
    void Application_Idle (object sender, EventArgs e)
    {
      while (glControl1.IsIdle)
      {
        glControl1.MakeCurrent();

        Simulate();
        Render();

        long now = DateTime.Now.Ticks;
        if (now - lastFpsTime > 5000000)      // more than 0.5 sec
        {
          lastFps = 0.5 * lastFps + 0.5 * (frameCounter * 1.0e7 / (now - lastFpsTime));
          lastPps = 0.5 * lastPps + 0.5 * (primitiveCounter * 1.0e7 / (now - lastFpsTime));
          lastFpsTime = now;
          frameCounter = 0;
          primitiveCounter = 0L;

          if (lastPps < 5.0e5)
            labelFps.Text = string.Format(CultureInfo.InvariantCulture, "Fps: {0:f1}, Pps: {1:f1}k",
                                           lastFps, (lastPps * 1.0e-3));
          else
            labelFps.Text = string.Format(CultureInfo.InvariantCulture, "Fps: {0:f1}, Pps: {1:f1}m",
                                           lastFps, (lastPps * 1.0e-6));

          if (puz != null)
            labelStatus.Text = string.Format(CultureInfo.InvariantCulture, "time: {0:f1}s, fr: {1}",
                                              puz.Time, puz.Frames);
        }

        // Pointing (nonempty pointOrigin means there was an unhandled debug-click).
        if (pointOrigin != null &&
             pointDirty)
        {
          Vector3d p0 = new Vector3d( pointOrigin.Value.X, pointOrigin.Value.Y, pointOrigin.Value.Z );
          Vector3d p1 = new Vector3d( pointTarget.X, pointTarget.Y, pointTarget.Z ) - p0;
          Vector2d uv;
          double nearest = double.PositiveInfinity;

          if (puz != null)
          {
            nearest = puz.Intersect(ref p0, ref p1, false);
          }
          else
          {
            // For test purposes only.
            Vector3d ul = new Vector3d( -1.0, -1.0, -1.0 );
            Vector3d size = new Vector3d( 2.0, 2.0, 2.0 );
            if (Geometry.RayBoxIntersection(ref p0, ref p1, ref ul, ref size, out uv))
              nearest = uv.X;
          }

          if (double.IsInfinity(nearest))
            spot = null;
          else
            spot = new Vector3((float)(p0.X + nearest * p1.X),
                                (float)(p0.Y + nearest * p1.Y),
                                (float)(p0.Z + nearest * p1.Z));
          pointDirty = false;
        }
      }
    }

    /// <summary>
    /// OpenGL init code.
    /// </summary>
    void InitOpenGL ()
    {
      // Log OpenGL info just for curiosity.
      GlInfo.LogGLProperties();

      // General OpenGL.
      glControl1.VSync = true;
      GL.ClearColor(Color.FromArgb(14, 20, 40));    // darker "navy blue"
      GL.Enable(EnableCap.DepthTest);
      GL.Enable(EnableCap.VertexProgramPointSize);
      GL.ShadeModel(ShadingModel.Flat);

      // VBO init:
      VBOid = new uint[2];           // one big buffer for vertex data, another buffer for tri/line indices
      GL.GenBuffers(2, VBOid);
      GlInfo.LogError("VBO init");
      VBOlen = new int[2];           // zeroes..

      // Texture.
      GenerateTexture();
    }

    // Generated texture.
    const int TEX_SIZE = 128;
    const int TEX_CHECKER_SIZE = 8;
    static Vector3 colWhite = new Vector3( 0.85f, 0.75f, 0.15f );
    static Vector3 colBlack = new Vector3( 0.15f, 0.15f, 0.60f );
    static Vector3 colShade = new Vector3( 0.15f, 0.15f, 0.15f );

    /// <summary>
    /// Texture handle.
    /// </summary>
    int texName = 0;

    /// <summary>
    /// Generate the texture.
    /// </summary>
    void GenerateTexture ()
    {
      GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
      texName = GL.GenTexture();
      GL.BindTexture(TextureTarget.Texture2D, texName);

      Vector3[] data = new Vector3[ TEX_SIZE * TEX_SIZE ];
      for (int y = 0; y < TEX_SIZE; y++)
        for (int x = 0; x < TEX_SIZE; x++)
        {
          int i = y * TEX_SIZE + x;
          bool odd = ((x / TEX_CHECKER_SIZE + y / TEX_CHECKER_SIZE) & 1) > 0;
          data[i] = odd ? colBlack : colWhite;
          // Add some fancy shading on the edges.
          if ((x % TEX_CHECKER_SIZE) == 0 || (y % TEX_CHECKER_SIZE) == 0)
            data[i] += colShade;
          if (((x + 1) % TEX_CHECKER_SIZE) == 0 || ((y + 1) % TEX_CHECKER_SIZE) == 0)
            data[i] -= colShade;
        }

      GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, TEX_SIZE, TEX_SIZE, 0, PixelFormat.Rgb, PixelType.Float, data);

      GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
      GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
      GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
      GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Linear);

      GlInfo.LogError("create-texture");
    }

    /// <summary>
    /// De-allocated all the data associated with the given texture object.
    /// </summary>
    /// <param name="texName"></param>
    void DestroyTexture (ref int texName)
    {
      int tHandle = texName;
      texName = 0;
      if (tHandle != 0)
        GL.DeleteTexture(tHandle);
    }

    static int Align (int address)
    {
      return (address + 15) & -16;
    }

    /// <summary>
    /// Reset VBO buffer's size.
    /// Forces InitDataBuffers() call next time buffers will be needed..
    /// </summary>
    void ResetDataBuffers ()
    {
      VBOlen[0] =
      VBOlen[1] = 0;
    }

    /// <summary>
    /// Initialize VBO buffers.
    /// Determine maximum buffer sizes and allocate VBO objects.
    /// Vertex buffer:
    /// <list type=">">
    /// <item>cube - triangles</item>
    /// </list>
    /// Index buffer:
    /// <list type=">">
    /// <item>cube - triangles</item>
    /// </list>
    /// </summary>
    unsafe void InitDataBuffers ()
    {
      puz.Dirty = false;

      // Init data buffers for current simulation state.
      // triangles: determine maximum stride, maximum vertices and indices
      float* ptr = null;
      uint* iptr = null;
      uint origin = 0;
      int stride = 0;

      // Vertex-buffer size.
      int maxVB;
      maxVB = Align(puz.cubes[0].TriangleVertices(ref ptr, ref origin, out stride, true, true, true, true));
      // maxVB contains maximal vertex-buffer size for all batches

      // Index-buffer size.
      int maxIB;
      maxIB = Align(puz.cubes[0].TriangleIndices(ref iptr, 0));
      // maxIB contains maximal index-buffer size for all batches

      VBOlen[0] = maxVB;
      VBOlen[1] = maxIB;

      // Vertex buffer in VBO[ 0 ].
      GL.BindBuffer(BufferTarget.ArrayBuffer, VBOid[0]);
      GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)VBOlen[0], IntPtr.Zero, BufferUsageHint.DynamicDraw);
      GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
      GlInfo.LogError("allocate vertex-buffer");

      // Index buffer in VBO[ 1 ].
      GL.BindBuffer(BufferTarget.ElementArrayBuffer, VBOid[1]);
      GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)VBOlen[1], IntPtr.Zero, BufferUsageHint.DynamicDraw);
      GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
      GlInfo.LogError("allocate index-buffer");
    }

    /// <summary>
    /// Simulation time of the last checkpoint in system ticks (100ns units)
    /// </summary>
    long ticksLast = DateTime.Now.Ticks;

    /// <summary>
    /// Simulation time of the last checkpoint in seconds.
    /// </summary>
    double timeLast = 0.0;

    /// <summary>
    /// Prime simulation init.
    /// </summary>
    private void InitSimulation (string param)
    {
      puz = new Puzzle();
      ResetSimulation(param);
    }

    /// <summary>
    /// [Re-]initialize the simulation.
    /// </summary>
    private void ResetSimulation (string param)
    {
      //Snapshots.ResetFrameNumber();
      if (puz != null)
        lock (puz)
        {
          ResetDataBuffers();
          puz.Reset(param);
        }

      // Global timer.
      ticksLast = DateTime.Now.Ticks;
      timeLast = 0.0;
    }

    /// <summary>
    /// Pause / restart simulation.
    /// </summary>
    private void PauseRestartSimulation ()
    {
      if (puz != null)
      {
        bool running = false;
        lock (puz)
          running = puz.Running = !puz.Running;

        buttonStart.Text = running ? "Stop" : "Start";
      }
    }

    /// <summary>
    /// Update Simulation parameters.
    /// </summary>
    private void UpdateSimulation ()
    {
      if (puz != null)
        lock (puz)
          puz.Update(textParam.Text);
    }

    /// <summary>
    /// Simulate one frame.
    /// </summary>
    private void Simulate ()
    {
      if (puz != null)
        lock (puz)
        {
          long nowTicks = DateTime.Now.Ticks;
          if (nowTicks > ticksLast)
          {
            if (puz.Running)
            {
              double timeScale = checkSlow.Checked ? Puzzle.slow : 1.0;
              timeLast += (nowTicks - ticksLast) * timeScale * 1.0e-7;
              puz.Simulate(timeLast);
            }
            ticksLast = nowTicks;
          }
        }
    }

    /// <summary>
    /// Handles mouse-button push.
    /// </summary>
    /// <returns>True if handled.</returns>
    bool MouseButtonDown (MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
        return false;

      // Rotation of the whole cube by an axis defined by the intersected face.
      mouseKeyPressCoordinatesX = e.X;
      mouseKeyPressCoordinatesY = e.Y;

      Vector3 pointO = screenToWorld( e.X, e.Y, 0.0f );
      Vector3 pointT = screenToWorld( e.X, e.Y, 1.0f );
      intersectVector1 = new Vector3d(pointO.X, pointO.Y, pointO.Z);
      intersectVector2 = new Vector3d(pointT.X, pointT.Y, pointT.Z) - intersectVector1;
      var result = puz.Intersect(ref intersectVector1, ref intersectVector2, out minCube);

      if (result == double.PositiveInfinity)
        intersectVector2 = Vector3d.Zero;

      return true;
    }

    /// <summary>
    /// Handles mouse-button release.
    /// </summary>
    /// <returns>True if handled.</returns>
    bool MouseButtonUp (MouseEventArgs e)
    {
      return false;
    }

    /// <summary>
    /// Handles mouse move.
    /// </summary>
    /// <returns>True if handled.</returns>
    bool MousePointerMove (object sender, MouseEventArgs e)
    {
      if (e.Button == System.Windows.Forms.MouseButtons.Right)
      {
        Control control = (Control) sender;
        if (control.Capture)
        {
          control.Capture = false;
        }
        if (control.ClientRectangle.Contains(e.Location) && intersectVector2 != Vector3d.Zero)
        {
          SelectionType selectionType;
          var type = GetRotationType(e, out selectionType);

          if (type == RotationType.YPositive || type == RotationType.YNegative)
            puz.SetRotation(puz.cubes, minCube, selectionType, type);

          else if (type == RotationType.ZPositive || type == RotationType.ZNegative)
            puz.SetRotation(puz.cubes, minCube, selectionType, type);

          else if (type == RotationType.XPositive || type == RotationType.XNegative)
            puz.SetRotation(puz.cubes, minCube, selectionType, type);

        }
      }
      return false;
    }

    /// <summary>
    /// Handles mouse wheel change.
    /// </summary>
    /// <returns>True if handled.</returns>
    bool MouseWheelChange (MouseEventArgs e)
    {
      return false;
    }

    /// <summary>
    /// Handles keyboard key release.
    /// </summary>
    /// <returns>True if handled.</returns>
    bool KeyHandle (KeyEventArgs e)
    {
      return false;
    }


    public RotationType GetRotationType (MouseEventArgs e, out SelectionType selectionType)
    {
      Vector3 pointO = screenToWorld( e.X, e.Y, 0.0f );
      Vector3 pointT = screenToWorld( e.X, e.Y, 1.0f );
      var actualScreenVector1 = new Vector3d(pointO.X, pointO.Y, pointO.Z);
      var actualScreenVector2 = new Vector3d(pointT.X, pointT.Y, pointT.Z) - actualScreenVector1;


      var xDiff = Math.Abs(intersectVector2.X - actualScreenVector2.X);
      var yDiff = Math.Abs(intersectVector2.Y - actualScreenVector2.Y);
      var zDiff = Math.Abs(intersectVector2.Z - actualScreenVector2.Z);

      var axisX = Math.Abs(intersectVector2.X);
      var axisY = Math.Abs(intersectVector2.Y);
      var axisZ = Math.Abs(intersectVector2.Z);

      if (xDiff > yDiff && xDiff > zDiff)
      {
        if (axisZ > axisY)
        {
          selectionType = SelectionType.Y;
          if (intersectVector1.Z >= 0)
          {
            if (actualScreenVector2.X > intersectVector2.X)
              return RotationType.XPositive;
            return RotationType.XNegative;
          }
          else
          {
            if (actualScreenVector2.X > intersectVector2.X)
              return RotationType.XNegative;
            return RotationType.XPositive;
          }
        }
        else
        {
          selectionType = SelectionType.Z;
          if (intersectVector1.Y >= 0)
          {
            if (actualScreenVector2.X > intersectVector2.X)
              return RotationType.XNegative;
            return RotationType.XPositive;
          }
          else
          {
            if (actualScreenVector2.X > intersectVector2.X)
              return RotationType.XPositive;
            return RotationType.XNegative;
          }
        }

      }
      else if (yDiff > xDiff && yDiff > zDiff)
      {
        if (axisZ > axisX)
        {
          selectionType = SelectionType.X;
          if (intersectVector1.Z >= 0)
          {
            if (actualScreenVector2.Y > intersectVector2.Y)
              return RotationType.YNegative;
            return RotationType.YPositive;
          }
          else
          {
            if (actualScreenVector2.Y > intersectVector2.Y)
              return RotationType.YPositive;
            return RotationType.YNegative;
          }
        }
        else
        {
          selectionType = SelectionType.Z;
          if (intersectVector1.X >= 0)
          {
            if (actualScreenVector2.Y > intersectVector2.Y)
              return RotationType.YPositive;
            return RotationType.YNegative;
          }
          else
          {
            if (actualScreenVector2.Y > intersectVector2.Y)
              return RotationType.YNegative;
            return RotationType.YPositive;
          }
        }
      }
      else if (zDiff > xDiff && zDiff > yDiff)
      {
        if (axisX > axisY)
        {
          selectionType = SelectionType.Y;
          if (intersectVector1.X >= 0)
          {
            if (actualScreenVector2.Z > intersectVector2.Z)
              return RotationType.ZNegative;
            return RotationType.ZPositive;
          }
          else
          {
            if (actualScreenVector2.Z > intersectVector2.Z)
              return RotationType.ZPositive;
            return RotationType.ZNegative;
          }
        }
        else
        {
          selectionType = SelectionType.X;
          if (intersectVector1.Y >= 0)
          {
            if (actualScreenVector2.Z > intersectVector2.Z)
              return RotationType.ZPositive;
            return RotationType.ZNegative;
          }
          else
          {
            if (actualScreenVector2.Z > intersectVector2.Z)
              return RotationType.ZNegative;
            return RotationType.ZPositive;
          }
        }
      }
      selectionType = SelectionType.Y;
      return RotationType.YNegative;
    }

    /// <summary>
    /// Render one frame.
    /// </summary>
    private void Render ()
    {
      if (!loaded)
        return;

      frameCounter++;

      GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
      GL.ShadeModel(checkSmooth.Checked ? ShadingModel.Smooth : ShadingModel.Flat);
      GL.PolygonMode(checkTwosided.Checked ? MaterialFace.FrontAndBack : MaterialFace.Front,
                      checkWireframe.Checked ? PolygonMode.Line : PolygonMode.Fill);
      if (checkTwosided.Checked)
        GL.Disable(EnableCap.CullFace);
      else
        GL.Enable(EnableCap.CullFace);

      tb.GLsetCamera();
      RenderScene();

      glControl1.SwapBuffers();
    }

    void EnableArrays (bool useTexture)
    {
      GL.EnableClientState(ArrayCap.VertexArray);
      if (useTexture)
        GL.EnableClientState(ArrayCap.TextureCoordArray);
      else
        GL.EnableClientState(ArrayCap.ColorArray);
    }

    void DisableArrays ()
    {
      GL.DisableClientState(ArrayCap.VertexArray);
      GL.DisableClientState(ArrayCap.TextureCoordArray);
      GL.DisableClientState(ArrayCap.ColorArray);
    }

    /// <summary>
    /// Rendering code itself (separated for clarity).
    /// </summary>
    void RenderScene ()
    {
      if (puz != null)
      {
        foreach (var item in puz.cubes)
        {
          if (VBOlen[0] == 0 ||
               VBOlen[1] == 0 ||
               puz.Dirty)
            InitDataBuffers();

          if (VBOlen[0] > 0 ||
               VBOlen[1] > 0)
          {
            // Texture handling.
            bool useTexture = checkTexture.Checked;
            if (texName == 0)
              useTexture = false;
            if (useTexture)
            {
              GL.Enable(EnableCap.Texture2D);
              GL.ActiveTexture(TextureUnit.Texture0);
              GL.BindTexture(TextureTarget.Texture2D, texName);
              GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Replace);
            }

            // Scene rendering from VBOs:
            EnableArrays(useTexture);

            // [txt] [colors] [normals] [ptsize] vertices
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOid[0]);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, VBOid[1]);
            int stride  = 0;      // stride for vertex arrays
            int indices = 0;      // number of indices for index arrays

            //-------------------------
            // Draw all triangles.

            IntPtr vertexPtr = GL.MapBuffer( BufferTarget.ArrayBuffer, BufferAccess.WriteOnly );
            IntPtr indexPtr  = GL.MapBuffer( BufferTarget.ElementArrayBuffer, BufferAccess.WriteOnly );
            unsafe
            {
              float* ptr = (float*)vertexPtr.ToPointer();
              uint* iptr = (uint*)indexPtr.ToPointer();
              indices = puz.FillTriangleData(item, ref ptr, ref iptr, out stride, useTexture, !useTexture, false, false);
            }
            GL.UnmapBuffer(BufferTarget.ArrayBuffer);
            GL.UnmapBuffer(BufferTarget.ElementArrayBuffer);
            IntPtr p = IntPtr.Zero;

            // Using FFP.
            if (useTexture)
            {
              GL.TexCoordPointer(2, TexCoordPointerType.Float, stride, p);
              p += Vector2.SizeInBytes;
            }

            if (!useTexture)
            {
              GL.ColorPointer(3, ColorPointerType.Float, stride, p);
              p += Vector3.SizeInBytes;
            }

            GL.VertexPointer(3, VertexPointerType.Float, stride, p);

            // Index buffer.
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, VBOid[1]);

            // Engage!
            GL.DrawElements(PrimitiveType.Triangles, indices, DrawElementsType.UnsignedInt, IntPtr.Zero);
            GlInfo.LogError("draw-elements-ffp");

            if (useTexture)
            {
              GL.BindTexture(TextureTarget.Texture2D, 0);
              GL.Disable(EnableCap.Texture2D);
            }

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            DisableArrays();
          }
        }
      }
      else
      {
        DisableArrays();

        // Default: draw trivial cube using immediate mode.

        GL.Begin(PrimitiveType.Quads);

        GL.Color3(0.0f, 1.0f, 0.0f);          // Set The Color To Green
        GL.Vertex3(1.0f, 1.0f, -1.0f);        // Top Right Of The Quad (Top)
        GL.Vertex3(-1.0f, 1.0f, -1.0f);       // Top Left Of The Quad (Top)
        GL.Vertex3(-1.0f, 1.0f, 1.0f);        // Bottom Left Of The Quad (Top)
        GL.Vertex3(1.0f, 1.0f, 1.0f);         // Bottom Right Of The Quad (Top)

        GL.Color3(1.0f, 0.5f, 0.0f);          // Set The Color To Orange
        GL.Vertex3(1.0f, -1.0f, 1.0f);        // Top Right Of The Quad (Bottom)
        GL.Vertex3(-1.0f, -1.0f, 1.0f);       // Top Left Of The Quad (Bottom)
        GL.Vertex3(-1.0f, -1.0f, -1.0f);      // Bottom Left Of The Quad (Bottom)
        GL.Vertex3(1.0f, -1.0f, -1.0f);       // Bottom Right Of The Quad (Bottom)

        GL.Color3(1.0f, 0.0f, 0.0f);          // Set The Color To Red
        GL.Vertex3(1.0f, 1.0f, 1.0f);         // Top Right Of The Quad (Front)
        GL.Vertex3(-1.0f, 1.0f, 1.0f);        // Top Left Of The Quad (Front)
        GL.Vertex3(-1.0f, -1.0f, 1.0f);       // Bottom Left Of The Quad (Front)
        GL.Vertex3(1.0f, -1.0f, 1.0f);        // Bottom Right Of The Quad (Front)

        GL.Color3(1.0f, 1.0f, 0.0f);          // Set The Color To Yellow
        GL.Vertex3(1.0f, -1.0f, -1.0f);       // Bottom Left Of The Quad (Back)
        GL.Vertex3(-1.0f, -1.0f, -1.0f);      // Bottom Right Of The Quad (Back)
        GL.Vertex3(-1.0f, 1.0f, -1.0f);       // Top Right Of The Quad (Back)
        GL.Vertex3(1.0f, 1.0f, -1.0f);        // Top Left Of The Quad (Back)

        GL.Color3(0.0f, 0.0f, 1.0f);          // Set The Color To Blue
        GL.Vertex3(-1.0f, 1.0f, 1.0f);        // Top Right Of The Quad (Left)
        GL.Vertex3(-1.0f, 1.0f, -1.0f);       // Top Left Of The Quad (Left)
        GL.Vertex3(-1.0f, -1.0f, -1.0f);      // Bottom Left Of The Quad (Left)
        GL.Vertex3(-1.0f, -1.0f, 1.0f);       // Bottom Right Of The Quad (Left)

        GL.Color3(1.0f, 0.0f, 1.0f);          // Set The Color To Violet
        GL.Vertex3(1.0f, 1.0f, -1.0f);        // Top Right Of The Quad (Right)
        GL.Vertex3(1.0f, 1.0f, 1.0f);         // Top Left Of The Quad (Right)
        GL.Vertex3(1.0f, -1.0f, 1.0f);        // Bottom Left Of The Quad (Right)
        GL.Vertex3(1.0f, -1.0f, -1.0f);       // Bottom Right Of The Quad (Right)

        GL.End();

        primitiveCounter += 12;
      }

      // Support: axes
      if (checkDebug.Checked)
      {
        float origWidth = GL.GetFloat( GetPName.LineWidth );
        float origPoint = GL.GetFloat( GetPName.PointSize );

        // Axes.
        GL.LineWidth(2.0f);
        GL.Begin(PrimitiveType.Lines);

        GL.Color3(1.0f, 0.1f, 0.1f);
        GL.Vertex3(center);
        GL.Vertex3(center + new Vector3(0.5f, 0.0f, 0.0f) * diameter);

        GL.Color3(0.0f, 1.0f, 0.0f);
        GL.Vertex3(center);
        GL.Vertex3(center + new Vector3(0.0f, 0.5f, 0.0f) * diameter);

        GL.Color3(0.2f, 0.2f, 1.0f);
        GL.Vertex3(center);
        GL.Vertex3(center + new Vector3(0.0f, 0.0f, 0.5f) * diameter);

        GL.End();

        // Support: pointing
        if (pointOrigin != null)
        {
          GL.Begin(PrimitiveType.Lines);
          GL.Color3(1.0f, 1.0f, 0.0f);
          GL.Vertex3(pointOrigin.Value);
          GL.Vertex3(pointTarget);
          GL.End();

          GL.PointSize(4.0f);
          GL.Begin(PrimitiveType.Points);
          GL.Color3(1.0f, 0.0f, 0.0f);
          GL.Vertex3(pointOrigin.Value);
          GL.Color3(0.0f, 1.0f, 0.2f);
          GL.Vertex3(pointTarget);
          if (spot != null)
          {
            GL.Color3(1.0f, 1.0f, 1.0f);
            GL.Vertex3(spot.Value);
          }
          GL.End();
        }

        // Support: frustum
        if (frustumFrame.Count >= 8)
        {
          GL.LineWidth(2.0f);
          GL.Begin(PrimitiveType.Lines);

          GL.Color3(1.0f, 0.0f, 0.0f);
          GL.Vertex3(frustumFrame[0]);
          GL.Vertex3(frustumFrame[1]);
          GL.Vertex3(frustumFrame[1]);
          GL.Vertex3(frustumFrame[3]);
          GL.Vertex3(frustumFrame[3]);
          GL.Vertex3(frustumFrame[2]);
          GL.Vertex3(frustumFrame[2]);
          GL.Vertex3(frustumFrame[0]);

          GL.Color3(1.0f, 1.0f, 1.0f);
          GL.Vertex3(frustumFrame[0]);
          GL.Vertex3(frustumFrame[4]);
          GL.Vertex3(frustumFrame[1]);
          GL.Vertex3(frustumFrame[5]);
          GL.Vertex3(frustumFrame[2]);
          GL.Vertex3(frustumFrame[6]);
          GL.Vertex3(frustumFrame[3]);
          GL.Vertex3(frustumFrame[7]);

          GL.Color3(0.0f, 1.0f, 0.0f);
          GL.Vertex3(frustumFrame[4]);
          GL.Vertex3(frustumFrame[5]);
          GL.Vertex3(frustumFrame[5]);
          GL.Vertex3(frustumFrame[7]);
          GL.Vertex3(frustumFrame[7]);
          GL.Vertex3(frustumFrame[6]);
          GL.Vertex3(frustumFrame[6]);
          GL.Vertex3(frustumFrame[4]);

          GL.End();
        }

        GL.LineWidth(origWidth);
        GL.PointSize(origPoint);
      }
    }
  }

  /// <summary>
  /// Cube object / primary scene - object able to be rendered,
  /// simulated (animated), realtime interaction with the user by mouse pointing.
  /// </summary>


  /// <summary>
  /// Puzzle instance.
  /// </summary>
  public class Puzzle
  {
    /// <summary>
    /// Simulated world = single cube.
    /// </summary>
    public List<Cube> cubes = new List<Cube>();

    ///Simulate just rotating cubes.
    public List<IEnumerable<Cube>> rotatingCubes = new List<IEnumerable<Cube>>();

    private RandomJames rnd = new RandomJames();

    /// <summary>
    /// Lock-protected simulation state.
    /// Pause-related stuff could be stored/handled elsewhere.
    /// </summary>
    public bool Running
    {
      get;
      set;
    }

    /// <summary>
    /// Number of simulated frames so far.
    /// </summary>
    public int Frames
    {
      get;
      private set;
    }

    /// <summary>
    /// Current sim-world time.
    /// </summary>
    public double Time
    {
      get;
      private set;
    }

    /// <summary>
    /// Significant change of simulation parameters .. need to reallocate buffers.
    /// </summary>
    public bool Dirty
    {
      get;
      set;
    }

    /// <summary>
    /// Slow motion coefficient.
    /// </summary>
    public static double slow = 0.25;

    /// <summary>
    /// Revolution speed in radians per second.
    /// </summary>
    public double speed = 1.0;

    /// <summary>
    /// Size of rubics cube in count of cubes in one row;
    /// </summary>
    int sizeOfRubicsCube = 5;


    /// <summary>
    /// Maximal coordinate of rubics cube.
    /// </summary>
    int maxCoordinate = 2;

    /// <summary>
    /// Minimal coordinate od rubics cube.
    /// </summary>
    int minCoordinate = -2;

    /// <summary>
    /// Decide if special animation should be engaged.
    /// </summary>
    bool specialAnimation = false;

    bool randomMoves = false;

    public Puzzle ()
    {
      Frames = 0;
      Time = 0.0;
      Running = true;
      Dirty = false;
      cubes = GenerateCubes();
    }


    public List<Cube> GenerateCubes ()
    {
      var cubes = new List<Cube> ();

      Vector3 colorFront = Vector3.Zero;
      Vector3 colorRight = Vector3.Zero;
      Vector3 colorBack = Vector3.Zero;
      Vector3 colorLeft = Vector3.Zero;
      Vector3 colorTop = Vector3.Zero;
      Vector3 colorBottom = Vector3.Zero;

      uint id = 0;
      double sizeOfCube = 0.9;
      int stepSize = 1;
      bool isEven = false;

      if(sizeOfRubicsCube % 2 == 1)
      {
        maxCoordinate = sizeOfRubicsCube / 2;
        minCoordinate = maxCoordinate * -1;
      }
      else
      {
        maxCoordinate = ((sizeOfRubicsCube / 2) * 2) - 1;
        minCoordinate = maxCoordinate * -1;
        sizeOfCube = 1.7;
        stepSize = 2;
        isEven = true;
      }


      for (int x = minCoordinate; x <= maxCoordinate; x += stepSize)
      {
        for (int y = minCoordinate; y <= maxCoordinate; y += stepSize)
        {
          for (int z = minCoordinate; z <= maxCoordinate; z += stepSize)
          {
            if (isEven && (x == 0 || y == 0 || z == 0))
              continue;

            if (((maxCoordinate - Math.Abs(x) < 1) ||
               (maxCoordinate - Math.Abs(y) < 1)  ||
               (maxCoordinate - Math.Abs(z) < 1)))
            {
              if (z == minCoordinate)
                colorBack = Vector3.UnitX + 0.5f * Vector3.UnitY;
              if (x == minCoordinate)
                colorLeft = Vector3.UnitY;
              if (y == minCoordinate)
                colorBottom = Vector3.UnitX + Vector3.UnitY;
              if (z == maxCoordinate)
                colorFront = Vector3.UnitX;
              if (x == maxCoordinate)
                colorRight = Vector3.UnitZ;
              if (y == maxCoordinate)
                colorTop = new Vector3(1.0f, 1.0f, 1.0f);


              cubes.Add(new Cube(new Vector3d(x, y, z), sizeOfCube, 0, id, colorFront, colorRight, colorBack, colorLeft, colorTop, colorBottom));

              colorFront = Vector3.Zero;
              colorRight = Vector3.Zero;
              colorBack = Vector3.Zero;
              colorLeft = Vector3.Zero;
              colorTop = Vector3.Zero;
              colorBottom = Vector3.Zero;

              id++;
            }
          }
        }
      }

      return cubes;
    }

    /// <summary>
    /// [Re-]initialize the simulation system.
    /// </summary>
    /// <param name="param">User-provided parameter string.</param>
    public void Reset (string param)
    {
      // Input params.
      Update(param);

      // Initialization job itself.
      Frames = 0;
      Time = 0.0;

      cubes = GenerateCubes();
    }

    /// <summary>
    /// Update simulation parameters.
    /// </summary>
    /// <param name="param">User-provided parameter string.</param>
    public void Update (string param)
    {
      // Input params.
      Dictionary<string, string> p = Util.ParseKeyValueList( param );
      if (p.Count == 0)
        return;

      // Animation: rotation speed.
      if (Util.TryParse(p, "speed", ref speed))
      {
        if (speed < 0.01)
          speed = 0.01;

        foreach (var cube in cubes)
          cube.speed = speed;
      }

      // Global: slow-motion coeff.
      if (!Util.TryParse(p, "slow", ref slow) ||
           slow < 1.0e-4)
        slow = 0.25;


      var prevSizeOfRubicsCube = sizeOfRubicsCube; 
      if (!Util.TryParse(p, "size", ref sizeOfRubicsCube))
      {
        if(sizeOfRubicsCube < 2)
          sizeOfRubicsCube = 2;
      }

      if(prevSizeOfRubicsCube != sizeOfRubicsCube)
      {
        cubes = GenerateCubes();
      }

      Util.TryParse(p, "specialAnimation", ref specialAnimation);
      Util.TryParse(p, "shuffle", ref randomMoves);
    }

    /// <summary>
    /// Do one step of simulation.
    /// </summary>
    /// <param name="time">Required target time.</param>
    public void Simulate (double time)
    {
      if (!Running)
        return;

      Frames++;
      if (randomMoves)
      {
        RandomRotate();
      }
      if (rotatingCubes.Count != 0)
      {
        for (int i = 0; i < rotatingCubes.Count; i++)
        {
          bool isClearRow = true;
          foreach (var cube in rotatingCubes[i])
          {
            if(cube.shouldAnimate == true)
              isClearRow = false;
          }

          if (isClearRow)
          {
            rotatingCubes.RemoveAt(i);
            i--;
            continue;
          }

          foreach (var cube in rotatingCubes[i])
            cube.Simulate(time, this, specialAnimation);
        }
      }
      Time = time;
    }

    public double Intersect (ref Vector3d p0, ref Vector3d p1, bool action = false)
    {
      Cube minCube;
      return Intersect(ref p0, ref p1, out minCube, false);
    }

    public double Intersect (ref Vector3d p0, ref Vector3d p1, out Cube minCube, bool action = false)
    {
      var selectedCubes = new List<Cube>();

      foreach (var cube in cubes)
      {
        var result = cube.Intersect(ref p0, ref p1, action);
        if (result != double.PositiveInfinity)
        {
          selectedCubes.Add(cube);
        }
      }

      minCube = FindNearestCube(selectedCubes, p0);
      if (selectedCubes.Count != 0)
        return 0;
      return double.PositiveInfinity;
    }

    public Cube FindNearestCube (List<Cube> selectedCubes, Vector3d p0)
    {
      double min = double.PositiveInfinity;
      Cube minCube = null;

      foreach (var cube in selectedCubes)
      {
        var xLength = cube.actualPosition.X - p0.X;
        var yLength = cube.actualPosition.Y - p0.Y;
        var zLength = cube.actualPosition.Z - p0.Z;

        var length = Math.Sqrt(Math.Pow(xLength, 2) + Math.Pow(yLength, 2) + Math.Pow(zLength,2));

        if (length < min)
        {
          min = length;
          minCube = cube;
        }
      }
      return minCube;
    }

    public void SetRotation (List<Cube> cubes, Cube cube, SelectionType coordinateType, RotationType rotationType)
    {
      var row = new List<Cube>();
      var axis = Vector3d.Zero;
      if (coordinateType == SelectionType.Y)
      {
        row = new List<Cube>(cubes.Where(x => x.actualPosition.Y == cube.actualPosition.Y));
        axis = new Vector3d(0, 1, 0);
      }
      else if (coordinateType == SelectionType.Z)
      {
        row = new List<Cube>(cubes.Where(x => x.actualPosition.Z == cube.actualPosition.Z));
        axis = new Vector3d(0, 0, 1);
      }
      else
      {
        row = new List<Cube>(cubes.Where(x => x.actualPosition.X == cube.actualPosition.X));
        axis = new Vector3d(1, 0, 0);
      }
      foreach (var cubeRow in row)
      {
        if (cubeRow.shouldAnimate == true)
        {
          ResetSelectedCubes(row);
          return;
        }

        cubeRow.axis = axis;
        cubeRow.shouldAnimate = true;
        cubeRow.rotationType = rotationType;
      }
      rotatingCubes.Add(row);
    }

    private void ResetSelectedCubes (IEnumerable<Cube> selectedCubes)
    {
      foreach (var cube in selectedCubes)
        if (cube.Visited == false)
          cube.shouldAnimate = false;
    }

    /// <summary>
    /// Prepares (fills) all the triangle-related data into the provided vertex buffer and index buffer.
    /// </summary>
    /// <returns>Number of used indices (to draw).</returns>
    public unsafe int FillTriangleData (Cube cube, ref float* ptr, ref uint* iptr, out int stride, bool txt, bool col, bool normal, bool ptsize)
    {
      // Original index pointer.
      uint* bakIptr = iptr;
      stride = 0;
      uint origin = 0;

      // Only one cube.
      uint bakOrigin = origin;
      cube.TriangleVertices(ref ptr, ref origin, out stride, txt, col, normal, ptsize);
      cube.TriangleIndices(ref iptr, bakOrigin);

      // Added indices.
      return (int)(iptr - bakIptr);
    }

    public void RandomRotate()
    {
      SelectionType previousType = SelectionType.X;

      var random1 = rnd.RandomInteger(0, 300);

      var randomSelected = (SelectionType)(random1 / 100);
      if(previousType == randomSelected || rotatingCubes.Count != 0)
      {
        return;
      }

      if(SelectionType.X == randomSelected)
      {
        for(int i = minCoordinate; i <= maxCoordinate; i++)
        {
          var random = rnd.RandomInteger(0,100);
          if ( random > 50)
          {
            if(random > 30)
            {
              SetRotation(cubes, cubes.Where(x => x.startPosition.X == i).First(), randomSelected, RotationType.YPositive);
            }
            else
            {
              SetRotation(cubes, cubes.Where(x => x.startPosition.X == i).First(), randomSelected, RotationType.YNegative);
            }
          }
        }
      }
      else if(SelectionType.Y == randomSelected)
      {
        for (int i = minCoordinate; i <= maxCoordinate; i++)
        {
          var random = rnd.RandomInteger(0,100);
          if (random > 50)
          {
            if (random > 30)
            {
              SetRotation(cubes, cubes.Where(x => x.startPosition.Y == i).First(), randomSelected, RotationType.XPositive);
            }
            else
            {
              SetRotation(cubes, cubes.Where(x => x.startPosition.Y == i).First(), randomSelected, RotationType.XNegative);
            }
          }
        }
      }
      else
      {
        for (int i = minCoordinate; i <= maxCoordinate; i++)
        {
          var random = rnd.RandomInteger(0,100);
          if (random > 50)
          {
            if (random > 30)
            {
              SetRotation(cubes, cubes.Where(x => x.startPosition.Z == i).First(), randomSelected, RotationType.YPositive);
            }
            else
            {
              SetRotation(cubes, cubes.Where(x => x.startPosition.Z == i).First(), randomSelected, RotationType.YNegative);
            }
          }
        }
      }
    }
  }
}
