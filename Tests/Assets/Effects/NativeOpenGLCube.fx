// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

TextureCube<float4> CubeTexture : register(t1);
sampler CubeTextureSampler : register(s1);

struct VertexInput
{
    float4 Position : POSITION0;
};

struct VertexOutput
{
    float4 Position : SV_Position;
};

VertexOutput VSMain(VertexInput input)
{
    VertexOutput output;
    output.Position = input.Position;
    return output;
}

float4 PSMain(VertexOutput input) : SV_Target0
{
    float3 direction;
    if (input.Position.x < 1.0)
        direction = float3(1, 0, 0);
    else if (input.Position.x < 2.0)
        direction = float3(-1, 0, 0);
    else if (input.Position.x < 3.0)
        direction = float3(0, 1, 0);
    else if (input.Position.x < 4.0)
        direction = float3(0, -1, 0);
    else if (input.Position.x < 5.0)
        direction = float3(0, 0, 1);
    else
        direction = float3(0, 0, -1);

    return CubeTexture.Sample(CubeTextureSampler, direction);
}

technique NativeOpenGLCube
{
    pass
    {
        VertexShader = compile vs_6_0 VSMain();
        PixelShader = compile ps_6_0 PSMain();
    }
}
