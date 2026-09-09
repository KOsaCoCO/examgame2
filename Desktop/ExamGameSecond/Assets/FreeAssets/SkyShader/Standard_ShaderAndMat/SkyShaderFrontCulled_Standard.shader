// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SkyShaderFrontCulled"
{
	Properties
	{
		_HighColor("HighColor", Color) = (1,1,0,0)
		_HighAdjust("HighAdjust", Range( -1 , 0)) = 0
		_MidColor("MidColor", Color) = (1,0.3706115,0,0)
		_MidAdjust("MidAdjust", Range( 0 , 1)) = 0.5
		_LowColor("LowColor", Color) = (0.3584906,0.07609471,0.07609471,0)
		_LowAdjust("LowAdjust", Range( -1 , 0)) = 0

	}
	
	SubShader
	{
		
		
		Tags { "RenderType"="Opaque" }
	LOD 100

		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend Off
		AlphaToMask Off
		Cull Front
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		
		
		
		Pass
		{
			Name "Unlit"
			Tags { "LightMode"="ForwardBase" }
			CGPROGRAM

			

			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			//only defining to not throw compilation error over Unity 5.5
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 worldPos : TEXCOORD0;
				#endif
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform float _HighAdjust;
			uniform float _MidAdjust;
			uniform float _LowAdjust;
			uniform float4 _HighColor;
			uniform float4 _MidColor;
			uniform float4 _LowColor;

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				o.ase_texcoord1.xy = v.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord1.zw = 0;
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
				vertexValue = v.vertex.xyz;
				#endif
				vertexValue = vertexValue;
				#if ASE_ABSOLUTE_VERTEX_POS
				v.vertex.xyz = vertexValue;
				#else
				v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				#endif
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 WorldPosition = i.worldPos;
				#endif
				float2 texCoord5 = i.ase_texcoord1.xy * float2( 1,1 ) + float2( 0,0 );
				float clampResult33 = clamp( ( _HighAdjust + ( texCoord5.y * 1.0 ) ) , 0.0 , 1.0 );
				float clampResult34 = clamp( ( ( 1.0 - texCoord5.y ) + _LowAdjust ) , 0.0 , 1.0 );
				float4 appendResult23 = (float4(clampResult33 , ( 1.0 - distance( texCoord5.y , _MidAdjust ) ) , clampResult34 , 0.0));
				float4 weightedBlendVar22 = appendResult23;
				float4 weightedAvg22 = ( ( weightedBlendVar22.x*_HighColor + weightedBlendVar22.y*_MidColor + weightedBlendVar22.z*_LowColor + weightedBlendVar22.w*float4( 0,0,0,0 ) )/( weightedBlendVar22.x + weightedBlendVar22.y + weightedBlendVar22.z + weightedBlendVar22.w ) );
				
				
				finalColor = weightedAvg22;
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18600
583;305;1906;694;1694.855;590.0959;1.3;True;False
Node;AmplifyShaderEditor.TextureCoordinatesNode;5;-807,-426.5;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;31;-529.1835,309.2111;Inherit;False;Property;_LowAdjust;LowAdjust;5;0;Create;True;0;0;False;0;False;0;-0.381;-1;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;19;-537,59.5;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;30;-530.5835,-485.7889;Inherit;False;Property;_HighAdjust;HighAdjust;1;0;Create;True;0;0;False;0;False;0;0;-1;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;35;-1037.055,-88.94591;Inherit;False;Property;_MidAdjust;MidAdjust;3;0;Create;True;0;0;False;0;False;0.5;0.128;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;18;-562,-377.5;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;32;-294.5835,222.2111;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;29;-284.5835,-452.7889;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DistanceOpNode;20;-754,-164.5;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;34;-158.5835,207.2111;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;21;-541,-159.5;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;33;-165.5835,-456.7889;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;13;-1083,-237.5;Inherit;False;Property;_MidColor;MidColor;2;0;Create;True;0;0;False;0;False;1,0.3706115,0,0;0,0.6133615,0.8588235,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;17;-1059,-430.5;Inherit;False;Property;_HighColor;HighColor;0;0;Create;True;0;0;False;0;False;1,1,0,0;0.9808721,1,0.3632075,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;23;5.788578,-382.1249;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ColorNode;12;-1087,-35.5;Inherit;False;Property;_LowColor;LowColor;4;0;Create;True;0;0;False;0;False;0.3584906,0.07609471,0.07609471,0;0,0.04240509,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WeightedBlendNode;22;318.3397,-278.5;Inherit;False;5;0;FLOAT4;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;571.3395,-283;Float;False;True;-1;2;ASEMaterialInspector;100;1;SkyShaderFrontCulled;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;True;0;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;False;False;False;False;False;False;True;0;False;-1;True;1;False;-1;True;True;True;True;True;0;False;-1;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;RenderType=Opaque=RenderType;True;2;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=ForwardBase;False;0;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;1;True;False;;False;0
WireConnection;19;0;5;2
WireConnection;18;0;5;2
WireConnection;32;0;19;0
WireConnection;32;1;31;0
WireConnection;29;0;30;0
WireConnection;29;1;18;0
WireConnection;20;0;5;2
WireConnection;20;1;35;0
WireConnection;34;0;32;0
WireConnection;21;0;20;0
WireConnection;33;0;29;0
WireConnection;23;0;33;0
WireConnection;23;1;21;0
WireConnection;23;2;34;0
WireConnection;22;0;23;0
WireConnection;22;1;17;0
WireConnection;22;2;13;0
WireConnection;22;3;12;0
WireConnection;0;0;22;0
ASEEND*/
//CHKSM=0D0E777AEDF676EEEE53BA1853A283304508AB81