Shader "Custom/DumpSoilShader"
{
	// Unity��Inspector�܂���C#�R�[�h���璲���ł���p�����[�^�̒�`
    Properties
    {
		[Header(Main Maps)]
	    [Space]
        
		_Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
		[Normal] _BumpMap("Normal Map", 2D) = "bump" {}
		_BumpScale("Normal Scale", Range(0,2)) = 1.0

		[Header(Surface Properties)]
		[Space]

		[Gamma] _Metallic("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
		
		[Header(Secondary Maps)]
		[Space]
		
		_DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}		
		[Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}
		_DetailNormalMapScale("Normal Scale", Range(0, 2)) = 1.0

		[Header(Dynamic Parameters)]
		[Space]
		_SoilBaseHeight("Soil Height", Range(0, 10)) = 1.0
		_SoilHeightMap("Soil Height Detail Map", 2D) = "black" {}
		_SoilHeightMapMaxHeight("Soil Height Map Max Height", Range(0, 2)) = 0.1
		_SoilSlideOffset("Soil Slide Offset", Float) = 0.0
		_TiltAngle("Current Tilt Angle", Range(0.0, 90.0)) = 0.0

		[Header(Form Detail Parameters)]
		_EdgeSlopeWidth("Edge Slope Width", Range(0.0, 0.5)) = 0.2
		_EdgeSlopeEaseInFactor("Edge Slop Ease In Factor", Range(0.0, 5.0)) = 2.0
		_EdgeSlopeDepth("Edge Slope Depth Factor", Range(0.0, 1.0)) = 0.5
    }

	// Surface�V�F�[�_�[�̃C���v���P�[�V����
    SubShader
    {
		// ������ݒ�
	    Tags {"Queue" = "Transparent" "RenderType" = "Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
        LOD 200

		// �V�F�[�_�[�R�[�h�̊J�n�|�C���g
		CGPROGRAM

		// ������ݒ肵�A�ȉ���vert(), surf()���\�b�h��o�^����
        #pragma surface surf Standard fullforwardshadows vertex:vert alpha:fade
        #pragma target 3.0
		
		// �ȏ��Properties�Ɠ��������p�����[�^
		fixed4 _Color;
        sampler2D _MainTex;
		sampler2D _BumpMap;
		half _BumpScale;
		half _Metallic;
		half _Glossiness;
		sampler2D _DetailAlbedoMap;
		sampler2D _DetailNormalMap;
		half _DetailNormalMapScale;
		
		half _SoilBaseHeight;
		sampler2D _SoilHeightMap;
		half _SoilHeightMapMaxHeight;
		half _SoilSlideOffset;
		half _TiltAngle;

		half _EdgeSlopeWidth = 0.2;
		half _EdgeSlopeEaseInFactor = 2.0;
		half _EdgeSlopeDepth = 0.5;

		// Unity�������I�ɐݒ肷��p�����[�^�F
		float4 _SoilHeightMap_TexelSize;

        struct Input
        {
			// Unity�������I�ɐݒ肷��f�[�^�F
            float2 uv_MainTex;
			float2 uv_BumpMap;
			float2 uv_DetailAlbedoMap;
			float2 uv_DetailNormalMap;
			float2 uv_SoilHeightMap;
			float3 worldNormal;
			// �}�j���A����vert()����ݒ肷��f�[�^�F
			float3 localPos;
        };

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

		// Unity��BlendNormals���\�b�h��萳�������@�Ńx�[�X�@���Əڍז@�����}�[�W���郁�\�b�h
		fixed3 blendNormalsAccurate(in fixed3 nBase, in fixed3 nDetail)
		{
			float3x3 nBasis = float3x3(
				float3(nBase.y, -nBase.x, nBase.z), // �����܂��-90�x�̉�]
				float3(nBase.x, nBase.y, nBase.z),
				float3(nBase.x, -nBase.z, nBase.y)); // x���܂��90�x�̉�]

			return normalize(nDetail.x * nBasis[0] + 
							 nDetail.y * nBasis[1] +
				             nDetail.z * nBasis[2]);
		}

		// HeightMap����@�����v�Z����
		float3 calcNormalFromHeightmap(float4 uv, float2 texelSize, float2 sampleOffset, float2 texelWorldSize, half height, bool clamp)
		{
			float4 sampleUV[4];
			sampleUV[0] = uv + float4(sampleOffset.y * float2(0, -1), 0, 0);
			sampleUV[1] = uv + float4(sampleOffset.x * float2(-1, 0), 0, 0);
			sampleUV[2] = uv + float4(sampleOffset.x * float2(1, 0), 0, 0);
			sampleUV[3] = uv + float4(sampleOffset.y * float2(0, 1), 0, 0);
			if (clamp)
				for (int i = 0; i < 4; ++i)
					sampleUV[i] = saturate(sampleUV[i]);

			float4 h;
			h[0] = tex2Dlod(_SoilHeightMap, sampleUV[0]).r * height;
			h[1] = tex2Dlod(_SoilHeightMap, sampleUV[1]).r * height;
			h[2] = tex2Dlod(_SoilHeightMap, sampleUV[2]).r * height;
			h[3] = tex2Dlod(_SoilHeightMap, sampleUV[3]).r * height;

			float2 texelsSeparation = float2(sampleUV[2].x - sampleUV[1].x, sampleUV[3].y - sampleUV[0].y) / texelSize;

			float3 n;
			n.x = (h[1] - h[2]) / (texelsSeparation.x * texelWorldSize.x);
			n.z = (h[0] - h[3]) / (texelsSeparation.y * texelWorldSize.y);
			n.y = 1;
			n = normalize(n);
			return n;
		}

		// �������̓y�����s�ړ��ɂ���āATexture��UV���I�t�Z�b�g����i�y�����x��\�����邽�߂Ɂj
		float2 soilSlidedUV(Input IN, float2 uv, float2 scale)
		{
			return uv + float2(0, _SoilSlideOffset * scale.y);
		}

		float easeInOutCubic(float x)
		{
			return x < 0.5 ? 4 * x * x * x : 1 - pow(-2 * x + 2, 3) / 2;
		}

		float easeInOutQuart(float x)
		{
			return x < 0.5 ? 8 * x * x * x * x : 1 - pow(-2 * x + 2, 4) / 2;
		}
		
		float inverseLerp(float a, float b, float t)
		{
			return (t - a) / (b - a);
		}

		// Vertex�̈ʒu�A�@�������߂�V�F�[�_
		void vert(inout appdata_full v, out Input o) {

			UNITY_INITIALIZE_OUTPUT(Input, o);

			if (v.vertex.y > 0.001)
			{
				float2 worldSize = float2(2.8, 2.7); // TODO

				// �y�������ɂ����Vertex���㎲�ɉ����Ĕz�u����
				v.vertex.y *= _SoilBaseHeight;
								
				// �y���\�ʂɏ����`��������悤�ɁAHeightMap�ɂ����Vertex�̍����������I�t�Z�b�g����
				float4 uv = float4(v.vertex.x + 0.5, v.vertex.z + 0.5, 0, 0); // z-comp is mip level
				uv.y += _SoilSlideOffset; // *2.1;
				v.vertex.y += tex2Dlod(_SoilHeightMap, uv).r * _SoilHeightMapMaxHeight;

				// ��L��Vertex�̍����I�t�Z�b�g�ɍ��킹�āA�@������]������
				float2 meshResolution = float2(33, 80);
				float2 meshCellTexelSize = 1.0 / (meshResolution - 1);
				float3 normalFromHeights = calcNormalFromHeightmap(uv, _SoilHeightMap_TexelSize.xy, meshCellTexelSize,
					_SoilHeightMap_TexelSize.xy * worldSize, _SoilHeightMapMaxHeight, false);
				v.normal = lerp(normalFromHeights, normalize(v.normal), saturate(pow(normalFromHeights.y, 3)));

				// �y���������[���Ƌ߂Â���ƁA�g�̂悤��Vertex�̍�����������
				bool useHeightShift = true;
				if (useHeightShift)
				{
					float heightShiftFrom = 0.04;
					float angleFactor = saturate(inverseLerp(0.0, 10.0, _TiltAngle));
					float s = saturate(inverseLerp(0.0, heightShiftFrom, _SoilBaseHeight));
					s = lerp(1, s, angleFactor);
					float u = lerp(2.0, 0.0, s);
					float t = 0.5 - v.vertex.z;
					t = saturate((t - 1.0) * u + 1.0);
					t = easeInOutQuart(t);
					v.vertex.y *= t * t * lerp(1.0, 3.0, 1.0 - s);
				}
			}

			// surf�V�F�[�_�[����g�����߂ɁAMesh���_�ɑ΂��đ��ΓI��Vertex�ʒu��ۑ�
			o.localPos = v.vertex.xyz;
		}

		// �F���O���[�X�P�[���ɕϊ�
		fixed3 greyscale(fixed3 color)
		{
			return (0.299 * color.r + 0.587 * color.g + 0.114 * color.b).rrr;
		}

		// Pixel�̐F�A�@���Ȃǂ����߂�V�F�[�_�[
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
			// UV���X�P�[���iTODO: �n�[�h�R�[�h����Ȃ��A_MainTex_ST��_DetailAlbedoMap_ST����擾�������j
			float2 uvScale = float2(0.3, 0.3) * 1.2;
			float2 uvScaleDetail = float2(3.0, 3.0) * 0.9;
			// �x�[�X�F��ݒ�
            fixed4 c = tex2D (_MainTex, soilSlidedUV(IN, IN.uv_MainTex, uvScale)) * _Color;
			o.Albedo = c.rgb;
			o.Albedo *= tex2D(_DetailAlbedoMap, soilSlidedUV(IN, IN.uv_DetailAlbedoMap, uvScaleDetail)).rgb * 2.0;
			o.Albedo = lerp(o.Albedo, greyscale(o.Albedo), 0.5) * 1.1;
			// �@����ݒ�
			o.Normal = UnpackScaleNormal(tex2D(_BumpMap, soilSlidedUV(IN, IN.uv_BumpMap, uvScale)), _BumpScale);
			fixed3 nDetail = UnpackScaleNormal(tex2D(_DetailNormalMap, soilSlidedUV(IN, IN.uv_DetailNormalMap, uvScaleDetail)), _DetailNormalMapScale);
			o.Normal = BlendNormals(o.Normal, nDetail);
			// ���̃}�e���A���ݒ�
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;

			// Pixel�����ɋ߂Â���ƃg�����X�y�A�����g�ɂ���
			o.Alpha = lerp(0.0, 1.0, saturate(IN.localPos.y / 0.002));
        }

		// �V�F�[�_�[�R�[�h�̏I���|�C���g
        ENDCG
    }
    FallBack "Diffuse"
}
