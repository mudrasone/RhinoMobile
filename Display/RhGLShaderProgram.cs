//
// RhGLShaderProgram.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 9/19/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Drawing;
using System.Text;
using OpenTK.Graphics.ES20;
using Rhino.Geometry;
using Rhino.DocObjects;


namespace RhinoMobile.Display
{
	public struct RhGLPredefinedUniforms
	{
		// ReSharper disable InconsistentNaming
		public int rglModelViewMatrix;
		public int rglProjectionMatrix;
		public int rglNormalMatrix;
		public int rglModelViewProjectionMatrix;

		public int rglDiffuse;
		public int rglSpecular;
		public int rglEmission;
		public int rglShininess;
		public int rglUsesColors;

		public int rglLightAmbient;
		public int rglLightDiffuse;
		public int rglLightSpecular;
		public int rglLightPosition;
		// ReSharper restore InconsistentNaming
	};

	public struct RhGLPredefinedAttributes
	{
		// ReSharper disable InconsistentNaming
		public int rglVertex;
		public int rglNormal;
		public int rglTexCoord0;
		public int rglColor;
		// ReSharper restore InconsistentNaming
	};

	public enum VertexAttributes : int
	{
		AttribVertex,
		AttribNormal,
		AttribTexcoord0,
		AttribColor,
		NumAttributes
	};

	public class RhGLShaderProgram
	{
		#region members
		public readonly int m_hProgram;
		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		RhGLPredefinedAttributes m_Attributes;
		RhGLPredefinedUniforms m_Uniforms;

		Transform m_MVXform;
		Transform m_MVPXform;
		#endregion

		/// <summary>
		/// Builds both the vertex and the fragment shaders
		/// Shaders MUST consist of both a vertex AND a fragment shader in 2.0.
		/// </summary>
		public static RhGLShaderProgram BuildProgram (string name, string vertexShader, string fragmentShader)
		{
			if (String.IsNullOrWhiteSpace (vertexShader) || String.IsNullOrWhiteSpace (fragmentShader))
				return null;

			int hVsh = BuildShader (vertexShader, ShaderType.VertexShader);
			int hFsh = BuildShader (fragmentShader, ShaderType.FragmentShader);

			if (hVsh == 0 || hFsh == 0)
				return null;

			int program_handle = GL.CreateProgram ();
			if (program_handle == 0 )
				return null;

			GL.AttachShader (program_handle, hVsh);
			GL.AttachShader (program_handle, hFsh);

			// These bindings are forced here so that mesh drawing can enable the
			// appropriate vertex array based on the same binding values. 
			// Note: These must be done before we attempt to link the program...
			// Note2: Rhino supports multiple textures but for now we'll only
			//        provide for a single set of texture coordinates.
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribVertex, "rglVertex");
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribNormal, "rglNormal");
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribTexcoord0, "rglTexCoord0");
			GL.BindAttribLocation (program_handle, (int)VertexAttributes.AttribColor, "rglColor");

			GL.LinkProgram (program_handle);

			int success;
			GL.GetProgram(program_handle, ProgramParameter.LinkStatus, out success);

			if (success == 0) {
				#if DEBUG
				int logLength;
				GL.GetProgram (program_handle, ProgramParameter.InfoLogLength, out logLength);
				if (logLength > 0) {
					string log = GL.GetProgramInfoLog (program_handle);
					System.Diagnostics.Debug.WriteLine (log);
				}
				#endif

				GL.DetachShader (program_handle, hVsh);
				GL.DetachShader (program_handle, hFsh);

				GL.DeleteProgram (program_handle);
				program_handle = 0;
			}

			GL.DeleteShader (hVsh);
			GL.DeleteShader (hFsh);

			if (program_handle == 0)
				return null;

			RhGLShaderProgram program = new RhGLShaderProgram (name, program_handle);
			program.ResolvePredefines ();

			return program;
		}

		#region properties
		public int Handle
		{
			get { return m_hProgram; }
		}

		public string Name { get; private set; }

		public int RglVertexIndex
		{
			get { return m_Attributes.rglVertex; }
		}

		public int RglNormalIndex
		{
			get { return m_Attributes.rglNormal; }
		}

		public int RglColorIndex
		{
			get { return m_Attributes.rglColor; }
		}

		public int RglNormalMatrix
		{
			get { return m_Uniforms.rglNormalMatrix; }
		}

		public int RglModelViewMatrix
		{
			get { return m_Uniforms.rglModelViewMatrix; }
		}

		public int RglProjectionMatrix
		{
			get { return m_Uniforms.rglProjectionMatrix; }
		}

		public int RglModelViewProjectionMatrix
		{
			get { return m_Uniforms.rglModelViewProjectionMatrix; }
		}
		#endregion

		#region constructors
		/// <summary>
		/// Only allow construction through the static BuildProgram function
		/// </summary>
		private RhGLShaderProgram (string name, int handle)
		{
			Name = name;
			m_hProgram = handle;
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Calls GL.UseProgram and the PreRun method
		/// </summary>
		public void Enable () 
		{
			GL.UseProgram (m_hProgram);
		}

		/// <summary>
		/// Calls PostRun and then sets the program to zero
		/// </summary>
		public void Disable () 
		{
			GL.UseProgram (0);
		}

		/// <summary>
		/// Given a Rhino light, modifies the uniforms accordingly...
		/// </summary>
		public void SetupLight (Light light)
		{
			if (m_Uniforms.rglLightAmbient >= 0) {
				float[] amb = { light.Ambient.R / 255.0f, light.Ambient.G / 255.0f, light.Ambient.B / 255.0f, 1.0f };
				GL.Uniform4 (m_Uniforms.rglLightAmbient, 1, amb);
			}

			if (m_Uniforms.rglLightDiffuse >= 0) {
				float[] diff = { light.Diffuse.R / 255.0f, light.Diffuse.G / 255.0f, light.Diffuse.B / 255.0f, 1.0f };
				GL.Uniform4 (m_Uniforms.rglLightDiffuse, 1, diff);
			}

			if (m_Uniforms.rglLightSpecular >= 0) {
				float[] spec = { light.Specular.R / 255.0f, light.Specular.G / 255.0f, light.Specular.B / 255.0f, 1.0f };
				GL.Uniform4 (m_Uniforms.rglLightSpecular, 1, spec);
			}

			if (m_Uniforms.rglLightPosition >= 0) {
				float[] pos = {
					(float)light.Direction.X,
					(float)light.Direction.Y,
					(float)light.Direction.Z
				};
				GL.Uniform3 (m_Uniforms.rglLightPosition, 1, pos);
			}
		}

		/// <summary>
		/// Sets up an ES2.0 material with a DisplayMaterial
		/// </summary>
		public void SetupMaterial (DisplayMaterial material)
		{
			float[] black = { 0.0f, 0.0f, 0.0f, 1.0f };
			float[] pspec = Convert.ToBoolean (material.Shine) ? material.SpecularColor : black;

			if (m_Uniforms.rglLightAmbient >= 0) {
				if (material.AmbientColor[3] > 0) {
					GL.Uniform4 (m_Uniforms.rglLightAmbient, 1, material.AmbientColor);
				} else
					GL.Uniform4 (m_Uniforms.rglLightAmbient, 1, black);
			}

			if (m_Uniforms.rglDiffuse >= 0)
				GL.Uniform4 (m_Uniforms.rglDiffuse, 1, material.DiffuseColor);

			if (m_Uniforms.rglSpecular >= 0)
				GL.Uniform4 (m_Uniforms.rglSpecular, 1, pspec);

			if (m_Uniforms.rglEmission >= 0) {
				GL.Uniform4 (m_Uniforms.rglEmission, 1, material.EmissionColor);
			}
			if (m_Uniforms.rglShininess >= 0)
				GL.Uniform1 (m_Uniforms.rglShininess,  material.Shine);

			if (m_Uniforms.rglUsesColors >= 0)
				GL.Uniform1 (m_Uniforms.rglUsesColors, 0);

			if (material.Alpha < 1.0)
				GL.Enable (EnableCap.Blend);
			else
				GL.Disable (EnableCap.Blend); 
		}

		/// <summary>
		/// Enables color usage in the shader
		/// </summary>
		public void EnableColorUsage (bool bEnable)
		{
			if (m_Uniforms.rglUsesColors >= 0)
				GL.Uniform1 (m_Uniforms.rglUsesColors, bEnable ? 1 : 0);
		}

		/// <summary>
		/// Sets up and initializes the viewport by setting the Uniforms
		/// </summary>
		public void SetupViewport (ViewportInfo viewport) 
		{
			Transform mv = new Transform();
			bool haveModelView = false;

			if (m_Uniforms.rglModelViewProjectionMatrix >= 0) {
				Transform mvp = viewport.GetXform (CoordinateSystem.World, CoordinateSystem.Clip);

				m_MVPXform = mvp;

				float[] modelViewProjection = mvp.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewProjectionMatrix, 1, false, modelViewProjection);
			}

			if (m_Uniforms.rglModelViewMatrix >= 0) {
				mv = viewport.GetXform (CoordinateSystem.World, CoordinateSystem.Camera);

				m_MVXform = mv;
				haveModelView = true;

				float[] modelView = mv.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewMatrix, 1, false, modelView);
			}

			if (m_Uniforms.rglProjectionMatrix >= 0) {
				Transform pr = viewport.GetXform (CoordinateSystem.Camera, CoordinateSystem.Clip);

				float[] projection = pr.ToFloatArray(false);
				GL.UniformMatrix4 (m_Uniforms.rglProjectionMatrix, 1, false, projection);
			}

			if (m_Uniforms.rglNormalMatrix >= 0) {

				float[] normalMatrix = new float[9];

				if (!haveModelView) {
					mv = viewport.GetXform (CoordinateSystem.World, CoordinateSystem.Camera);
					m_MVXform = mv;
				}
					
				Matrix4Dto3F (mv, ref normalMatrix, false);
				GL.UniformMatrix3 (m_Uniforms.rglNormalMatrix, 1, false, normalMatrix);
			}

		}

		/// <summary>
		/// Used to sets the ModelViewMatrix transforms for instance transforms
		/// </summary>
		public void SetModelViewMatrix (Transform xform)
		{
			Transform mv;

			if (m_Uniforms.rglModelViewProjectionMatrix >= 0) {
				Transform mvp = m_MVPXform;

				mvp = mvp * xform;

				float[] modelViewProjection = mvp.ToFloatArray (false);
				GL.UniformMatrix4 (m_Uniforms.rglModelViewProjectionMatrix, 1, false, modelViewProjection);
			}

			if (m_Uniforms.rglModelViewMatrix >= 0) {
				mv = m_MVXform * xform;

				float[] modelView = mv.ToFloatArray(false);

				GL.UniformMatrix4 (m_Uniforms.rglModelViewMatrix,
				                   1,	
				                   false,
													 modelView);
			}

			if (m_Uniforms.rglNormalMatrix >= 0) {
				float[] normalMatrix = new float[9];

				mv = m_MVXform * xform;

				Matrix4Dto3F (mv, ref normalMatrix, false);
				GL.UniformMatrix3 (m_Uniforms.rglNormalMatrix, 1, false, normalMatrix);
			}
		}
		#endregion

		#region Protected Methods
		/// <summary>
		/// Compiles the shader source with a type.  In Debug, produces error log if needed.
		/// </summary>
		static int BuildShader (string source, ShaderType type)
		{
			int hShader = GL.CreateShader (type);

			GL.ShaderSource (hShader, source);
			GL.CompileShader (hShader);

			int success;
			GL.GetShader (hShader, ShaderParameter.CompileStatus, out success);

			if (success == 0) {
				#if DEBUG
				int logLength;
				GL.GetShader (hShader, ShaderParameter.InfoLogLength, out logLength);
				if (logLength > 0) {
					string log = GL.GetShaderInfoLog ((int)hShader);
					System.Diagnostics.Debug.WriteLine (log);
				}
				#endif

				GL.DeleteShader (hShader);
				hShader = 0;
			}

			return hShader;
		}

		/// <summary>
		/// Resolves all attributes and uniforms in the shader
		/// </summary>
		public void ResolvePredefines ()
		{
			#if __ANDROID__
			// The following bifurcation is due to differences between OpenTK-1.0 on
			// MonoTouch and MonoDroid.  GL.GetAttribLocation has a different method
			// signature on each platform.  Remove the following in favor of the iOS
			// version when this is corrected by Xamarin.
			StringBuilder rglVertex = new StringBuilder ("rglVertex");
			StringBuilder rglNormal = new StringBuilder ("rglNormal");
			StringBuilder rglTexCoord0 = new StringBuilder ("rglTexCoord0");
			StringBuilder rglColor = new StringBuilder ("rglColor");

			m_Attributes.rglVertex = GL.GetAttribLocation (m_hProgram, rglVertex);
			m_Attributes.rglNormal = GL.GetAttribLocation (m_hProgram, rglNormal);
			m_Attributes.rglTexCoord0 = GL.GetAttribLocation (m_hProgram, rglTexCoord0);
			m_Attributes.rglColor = GL.GetAttribLocation (m_hProgram, rglColor);

			StringBuilder rglModelViewMatrix = new StringBuilder ("rglModelViewMatrix");
			StringBuilder rglProjectionMatrix = new StringBuilder ("rglProjectionMatrix");
			StringBuilder rglNormalMatrix = new StringBuilder ("rglNormalMatrix");
			StringBuilder rglModelViewProjectionMatrix = new StringBuilder ("rglModelViewProjectionMatrix");

			m_Uniforms.rglModelViewMatrix = GL.GetUniformLocation (m_hProgram, rglModelViewMatrix);
			m_Uniforms.rglProjectionMatrix = GL.GetUniformLocation (m_hProgram, rglProjectionMatrix);
			m_Uniforms.rglNormalMatrix = GL.GetUniformLocation (m_hProgram, rglNormalMatrix);
			m_Uniforms.rglModelViewProjectionMatrix = GL.GetUniformLocation (m_hProgram, rglModelViewProjectionMatrix);

			StringBuilder rglDiffuse = new StringBuilder ("rglDiffuse");
			StringBuilder rglSpecular = new StringBuilder ("rglSpecular");
			StringBuilder rglEmission = new StringBuilder ("rglEmission");
			StringBuilder rglShininess = new StringBuilder ("rglShininess");
			StringBuilder rglUsesColors = new StringBuilder ("rglUsesColors");

			m_Uniforms.rglDiffuse = GL.GetUniformLocation (m_hProgram, rglDiffuse);
			m_Uniforms.rglSpecular = GL.GetUniformLocation (m_hProgram, rglSpecular);
			m_Uniforms.rglEmission = GL.GetUniformLocation (m_hProgram, rglEmission);
			m_Uniforms.rglShininess = GL.GetUniformLocation (m_hProgram, rglShininess);
			m_Uniforms.rglUsesColors = GL.GetUniformLocation (m_hProgram, rglUsesColors);

			StringBuilder rglLightAmbient = new StringBuilder ("rglLightAmbient");
			StringBuilder rglLightDiffuse = new StringBuilder ("rglLightDiffuse");
			StringBuilder rglLightSpecular = new StringBuilder ("rglLightSpecular");
			StringBuilder rglLightPosition = new StringBuilder ("rglLightPosition");

			m_Uniforms.rglLightAmbient = GL.GetUniformLocation(m_hProgram, rglLightAmbient);
			m_Uniforms.rglLightDiffuse = GL.GetUniformLocation(m_hProgram, rglLightDiffuse);
			m_Uniforms.rglLightSpecular = GL.GetUniformLocation(m_hProgram, rglLightSpecular);
			m_Uniforms.rglLightPosition = GL.GetUniformLocation(m_hProgram, rglLightPosition);
			#endif

			#if __IOS__
			m_Attributes.rglVertex = GL.GetAttribLocation (m_hProgram, "rglVertex");
			m_Attributes.rglNormal = GL.GetAttribLocation (m_hProgram, "rglNormal");
			m_Attributes.rglTexCoord0 = GL.GetAttribLocation (m_hProgram, "rglTexCoord0");
			m_Attributes.rglColor = GL.GetAttribLocation (m_hProgram, "rglColor");

			m_Uniforms.rglModelViewMatrix = GL.GetUniformLocation (m_hProgram, "rglModelViewMatrix");
			m_Uniforms.rglProjectionMatrix = GL.GetUniformLocation (m_hProgram, "rglProjectionMatrix");
			m_Uniforms.rglNormalMatrix = GL.GetUniformLocation (m_hProgram, "rglNormalMatrix");
			m_Uniforms.rglModelViewProjectionMatrix = GL.GetUniformLocation (m_hProgram, "rglModelViewProjectionMatrix");

			m_Uniforms.rglDiffuse = GL.GetUniformLocation (m_hProgram, "rglDiffuse");
			m_Uniforms.rglSpecular = GL.GetUniformLocation (m_hProgram, "rglSpecular");
			m_Uniforms.rglEmission = GL.GetUniformLocation (m_hProgram, "rglEmission");
			m_Uniforms.rglShininess = GL.GetUniformLocation (m_hProgram, "rglShininess");
			m_Uniforms.rglUsesColors = GL.GetUniformLocation (m_hProgram, "rglUsesColors");

			m_Uniforms.rglLightAmbient = GL.GetUniformLocation(m_hProgram, "rglLightAmbient");
			m_Uniforms.rglLightDiffuse = GL.GetUniformLocation(m_hProgram, "rglLightDiffuse");
			m_Uniforms.rglLightSpecular = GL.GetUniformLocation(m_hProgram, "rglLightSpecular");
			m_Uniforms.rglLightPosition = GL.GetUniformLocation(m_hProgram, "rglLightPosition");
			#endif
		}
		#endregion

		#region Utility Methods
		/// <summary>
		/// Converts a double-precision 4x4 Matrix into a floating point array.
		/// </summary>
		private static void Matrix4Dto3F (Transform d, ref float [] f, bool rowDominant)
		{
			if (rowDominant) {
				f [0] = (float)d.M00; 
				f [1] = (float)d.M01;
				f [2] = (float)d.M02;
				f [3] = (float)d.M10;
				f [4] = (float)d.M11;
				f [5] = (float)d.M12;
				f [6] = (float)d.M20;
				f [7] = (float)d.M21;
				f [8] = (float)d.M22;
			} else {
				f [0] = (float)d.M00;
				f [1] = (float)d.M10;
				f [2] = (float)d.M20;
				f [3] = (float)d.M01;
				f [4] = (float)d.M11;
				f [5] = (float)d.M21;
				f [6] = (float)d.M02;
				f [7] = (float)d.M12;
				f [8] = (float)d.M22;
			}
		}
		#endregion

	}
}