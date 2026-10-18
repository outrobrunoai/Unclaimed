#ifndef UNCLAIMED_VEGETACAO_VENTO_INCLUDED
#define UNCLAIMED_VEGETACAO_VENTO_INCLUDED
// RGBA linear: branch flexibility, branch/leaf phase, leaf flutter, quadratic main bend.
// Amplitudes are WORLD metres. Horizontal/normal displacement <=
// (_VentoTronco + _VentoRamos + _VentoFolhas) * (base + gust).
// Length correction adds a downward displacement <= horizontalDisplacement^2 / (2 * height).
float4 _VentoDirForca, _VentoRajada;
float VentoHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float VentoRuido(float2 p)
{
    float2 c = floor(p), f = frac(p);
    f = f*f*f*(f*(f*6-15)+10); // Quintic interpolation: no abrupt velocity/acceleration changes.
    return lerp(lerp(VentoHash(c), VentoHash(c+float2(1,0)), f.x),
        lerp(VentoHash(c+float2(0,1)), VentoHash(c+1), f.x), f.y);
}
float VentoOnda(float t, float phase)
{
    return sin(t + phase + (VentoRuido(float2(t * 0.12, phase))-0.5)*1.2);
}
float3 DeformarVegetacao(float3 positionOS, float3 normalOS, float4 color)
{
    float3 world = TransformObjectToWorld(positionOS);
    float2 dir = _VentoDirForca.xy;
    float time = _VentoDirForca.w;
    float height = max(0.05, _VentoAltura);
    if (_VentoSemCor > 0.5)
    {
        float a = saturate(positionOS.y / height); a *= a;
        // Flutter also fades at the root, so fallback meshes remain firmly planted.
        color = float4(a, VentoHash(positionOS.xz + floor(positionOS.y * 3)), a * _VentoFolhaSemCor, a);
    }
    color = saturate(color);
    float2 front = (world.xz - dir * time * _VentoRajada.y) / max(0.1, _VentoRajada.x);
    float gust = smoothstep(lerp(0.15, 0.94, _VentoRajada.w), 1.0, VentoRuido(front)) * _VentoRajada.z;
    float force = _VentoDirForca.z + gust;
    float3 origin = UNITY_MATRIX_M._m03_m13_m23;
    float phase = (VentoHash(origin.xz) + color.g) * TWO_PI;
    float t = time * max(0, _VentoFrequencia) * TWO_PI;
    float main = force * _VentoTronco * color.a * (0.65 + 0.35 * VentoOnda(t, phase));
    float2 side = float2(-dir.y, dir.x);
    float2 branchDir = (dir + side * 0.25 * sin(phase)) / 1.031;
    float2 horizontal = dir * main + branchDir * force * _VentoRamos * color.r * VentoOnda(t * 1.6, phase + 1.7);
    float3 offset = float3(horizontal.x, -dot(horizontal, horizontal) / (2 * height), horizontal.y);
    offset += TransformObjectToWorldNormal(normalOS) * force * _VentoFolhas * color.b * VentoOnda(t * 4, phase + 3.1);
    return TransformWorldToObject(world + offset);
}
#endif
