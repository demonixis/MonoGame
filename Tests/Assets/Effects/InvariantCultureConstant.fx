#include "Include.fxh"

struct VertexShaderOutput
{
    float4 Position : SV_Position;
};

VertexShaderOutput VertexShaderFunction(float4 position : POSITION0)
{
    VertexShaderOutput output;
    output.Position = position;
    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : SV_Target0
{
    return float4(0.333333343, 0.5, 0.25, 1.0);
}

technique InvariantCultureConstant
{
    pass Pass0
    {
        VertexShader = compile VS_PROFILE VertexShaderFunction();
        PixelShader = compile PS_PROFILE PixelShaderFunction();
    }
}
