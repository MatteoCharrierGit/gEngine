#version 330

in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

// Forniti automaticamente da raylib per il material corrente.
uniform sampler2D texture0;   // mappa albedo (bianca di default)
uniform vec4 colDiffuse;      // colore albedo del material (maps[albedo].color)

#define MAX_LIGHTS 7
#define LIGHT_DIRECTIONAL 0
#define LIGHT_POINT 1

struct Light {
    int enabled;
    int type;
    vec3 position;
    vec3 target;    // per la direzionale: direzione della luce
    vec3 color;
    float intensity;
};

uniform Light lights[MAX_LIGHTS];
uniform vec3 viewPos;
uniform vec3 ambient;
uniform float metallic;
uniform float roughness;

out vec4 finalColor;

const float PI = 3.14159265359;

float DistributionGGX(vec3 N, vec3 H, float rough)
{
    float a = rough*rough;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;
    float denom = NdotH2*(a2 - 1.0) + 1.0;
    denom = PI*denom*denom;
    return a2/max(denom, 0.0001);
}

float GeometrySchlickGGX(float NdotV, float rough)
{
    float r = rough + 1.0;
    float k = (r*r)/8.0;
    return NdotV/(NdotV*(1.0 - k) + k);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float rough)
{
    return GeometrySchlickGGX(max(dot(N, V), 0.0), rough)*GeometrySchlickGGX(max(dot(N, L), 0.0), rough);
}

vec3 FresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0)*pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

void main()
{
    vec4 albedoTex = texture(texture0, fragTexCoord);

    // texture0 e colDiffuse sono in spazio sRGB, il lighting qui sotto vuole valori
    // lineari. Senza questa conversione la pow(1/2.2) in fondo applica la gamma una
    // seconda volta e l'immagine esce slavata e senza contrasto.
    // fragColor resta com'è: per la spec glTF i vertex color sono già lineari.
    vec3 albedo = pow(albedoTex.rgb, vec3(2.2))*pow(colDiffuse.rgb, vec3(2.2))*fragColor.rgb;

    vec3 N = normalize(fragNormal);
    vec3 V = normalize(viewPos - fragPosition);
    vec3 F0 = mix(vec3(0.04), albedo, metallic);

    vec3 Lo = vec3(0.0);

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (lights[i].enabled == 0) continue;

        vec3 L;
        float attenuation = 1.0;

        if (lights[i].type == LIGHT_DIRECTIONAL)
        {
            L = normalize(-lights[i].target);
        }
        else
        {
            vec3 toLight = lights[i].position - fragPosition;
            float dist = length(toLight);
            L = toLight/max(dist, 0.0001);
            attenuation = 1.0/(dist*dist);
        }

        vec3 H = normalize(V + L);
        vec3 radiance = lights[i].color*lights[i].intensity*attenuation;

        float NDF = DistributionGGX(N, H, roughness);
        float G = GeometrySmith(N, V, L, roughness);
        vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

        vec3 numerator = NDF*G*F;
        float denominator = 4.0*max(dot(N, V), 0.0)*max(dot(N, L), 0.0) + 0.0001;
        vec3 specular = numerator/denominator;

        vec3 kD = (vec3(1.0) - F)*(1.0 - metallic);

        float NdotL = max(dot(N, L), 0.0);
        Lo += (kD*albedo/PI + specular)*radiance*NdotL;
    }

    vec3 color = ambient*albedo + Lo;

    // Reinhard tone mapping + gamma correction.
    color = color/(color + vec3(1.0));
    color = pow(color, vec3(1.0/2.2));

    finalColor = vec4(color, albedoTex.a*colDiffuse.a);
}
