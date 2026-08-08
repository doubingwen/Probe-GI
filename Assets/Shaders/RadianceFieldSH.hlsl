#ifndef DOU_GI_RADIANCE_FIELD_SH_INCLUDED
#define DOU_GI_RADIANCE_FIELD_SH_INCLUDED

#define DOU_GI_FIXED_POINT_SCALE 100000.0
#define DOU_GI_SH_COEFFICIENT_COUNT 9
#define DOU_GI_SH_RGB_VALUE_COUNT 27

float EvaluateShBasis(int coefficientIndex, float3 direction)
{
    const float k0 = 0.2820947918;
    const float k1 = 0.4886025119;
    const float k2 = 1.0925484306;
    const float k3 = 0.3153915652;
    const float k4 = 0.5462742153;

    // Preserve the project's Y-up basis convention.
    float x = direction.x;
    float y = direction.z;
    float z = direction.y;

    if (coefficientIndex == 0) return k0;
    if (coefficientIndex == 1) return k1 * y;
    if (coefficientIndex == 2) return k1 * z;
    if (coefficientIndex == 3) return k1 * x;
    if (coefficientIndex == 4) return k2 * x * y;
    if (coefficientIndex == 5) return k2 * y * z;
    if (coefficientIndex == 6) return k3 * (2.0 * z * z - x * x - y * y);
    if (coefficientIndex == 7) return k2 * x * z;
    if (coefficientIndex == 8) return k4 * (x * x - y * y);
    return 0.0;
}

float3 EvaluateDiffuseIrradiance(float3 coefficients[DOU_GI_SH_COEFFICIENT_COUNT], float3 normal)
{
    const float convolution[3] = { 3.14159265, 2.09439510, 0.78539816 };
    float3 irradiance = EvaluateShBasis(0, normal) * coefficients[0] * convolution[0];

    [unroll]
    for (int index = 1; index <= 3; index++)
        irradiance += EvaluateShBasis(index, normal) * coefficients[index] * convolution[1];

    [unroll]
    for (int index = 4; index < DOU_GI_SH_COEFFICIENT_COUNT; index++)
        irradiance += EvaluateShBasis(index, normal) * coefficients[index] * convolution[2];

    return max(irradiance, 0.0);
}

int EncodeRadiance(float value)
{
    return (int)(value * DOU_GI_FIXED_POINT_SCALE);
}

float DecodeRadiance(int value)
{
    return (float)value / DOU_GI_FIXED_POINT_SCALE;
}

int3 WorldToFieldCell(float3 worldPosition, float spacing, float3 origin)
{
    return (int3)floor((worldPosition - origin) / spacing);
}

bool IsFieldCellValid(int3 cell, int3 dimensions)
{
    return all(cell >= 0) && all(cell < dimensions);
}

int FlattenFieldCell(int3 cell, int3 dimensions)
{
    return cell.x * dimensions.y * dimensions.z + cell.y * dimensions.z + cell.z;
}

float3 FieldCellPosition(int3 cell, float spacing, float3 origin)
{
    return (float3)cell * spacing + origin;
}

void LoadProbeSh(
    StructuredBuffer<int> fieldCoefficients,
    int probeIndex,
    out float3 coefficients[DOU_GI_SH_COEFFICIENT_COUNT])
{
    int baseOffset = probeIndex * DOU_GI_SH_RGB_VALUE_COUNT;
    [unroll]
    for (int coefficientIndex = 0; coefficientIndex < DOU_GI_SH_COEFFICIENT_COUNT; coefficientIndex++)
    {
        int valueOffset = baseOffset + coefficientIndex * 3;
        coefficients[coefficientIndex] = float3(
            DecodeRadiance(fieldCoefficients[valueOffset]),
            DecodeRadiance(fieldCoefficients[valueOffset + 1]),
            DecodeRadiance(fieldCoefficients[valueOffset + 2]));
    }
}

float TrilinearBlend(float values[8], float3 blend)
{
    float x00 = lerp(values[0], values[4], blend.x);
    float x10 = lerp(values[2], values[6], blend.x);
    float x01 = lerp(values[1], values[5], blend.x);
    float x11 = lerp(values[3], values[7], blend.x);
    return lerp(lerp(x00, x10, blend.y), lerp(x01, x11, blend.y), blend.z);
}

float3 TrilinearBlend(float3 values[8], float3 blend)
{
    float3 x00 = lerp(values[0], values[4], blend.x);
    float3 x10 = lerp(values[2], values[6], blend.x);
    float3 x01 = lerp(values[1], values[5], blend.x);
    float3 x11 = lerp(values[3], values[7], blend.x);
    return lerp(lerp(x00, x10, blend.y), lerp(x01, x11, blend.y), blend.z);
}

float3 SampleRadianceField(
    float3 worldPosition,
    float3 albedo,
    float3 normal,
    StructuredBuffer<int> fieldCoefficients,
    float spacing,
    float3 origin,
    int3 dimensions)
{
    static const int3 cornerOffsets[8] =
    {
        int3(0, 0, 0), int3(0, 0, 1), int3(0, 1, 0), int3(0, 1, 1),
        int3(1, 0, 0), int3(1, 0, 1), int3(1, 1, 0), int3(1, 1, 1)
    };

    int3 baseCell = WorldToFieldCell(worldPosition, spacing, origin);
    float3 reflectedRadiance[8];
    float normalWeights[8];

    [unroll]
    for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
    {
        int3 cell = baseCell + cornerOffsets[cornerIndex];
        reflectedRadiance[cornerIndex] = 0.0;
        normalWeights[cornerIndex] = 0.0;
        if (!IsFieldCellValid(cell, dimensions))
            continue;

        float3 toProbe = FieldCellPosition(cell, spacing, origin) - worldPosition;
        float normalWeight = saturate(dot(normalize(toProbe), normal));
        float3 sh[DOU_GI_SH_COEFFICIENT_COUNT];
        LoadProbeSh(fieldCoefficients, FlattenFieldCell(cell, dimensions), sh);

        normalWeights[cornerIndex] = normalWeight;
        reflectedRadiance[cornerIndex] = EvaluateDiffuseIrradiance(sh, normal) * (albedo / PI) * normalWeight;
    }

    float3 basePosition = FieldCellPosition(baseCell, spacing, origin);
    float3 blend = saturate((worldPosition - basePosition) / spacing);
    float blendedWeight = TrilinearBlend(normalWeights, blend);
    return TrilinearBlend(reflectedRadiance, blend) / max(blendedWeight, 0.0005);
}

#endif
