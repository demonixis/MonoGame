// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

struct VertexInput
{
    float4 Position : POSITION0;
};

struct VertexOutput
{
    float4 Position : SV_Position;
};

struct PixelOutput
{
    float4 Target0 : SV_Target0;
    float4 Target1 : SV_Target1;
    float4 Target2 : SV_Target2;
    float4 Target3 : SV_Target3;
};

VertexOutput VSMain(VertexInput input)
{
    VertexOutput output;
    output.Position = input.Position;
    return output;
}

PixelOutput PSMain()
{
    PixelOutput output;
    output.Target0 = float4(1, 0, 0, 1);
    output.Target1 = float4(0, 1, 0, 1);
    output.Target2 = float4(0, 0, 1, 1);
    output.Target3 = float4(1, 1, 0, 1);
    return output;
}

technique NativeOpenGLMrt
{
    pass
    {
        VertexShader = compile vs_6_0 VSMain();
        PixelShader = compile ps_6_0 PSMain();
    }
}
