#version 330

in vec2 fragTexCoord;
in vec4 fragColor;

// Forniti automaticamente da raylib per il material corrente.
uniform sampler2D texture0;   // mappa albedo (bianca di default)
uniform vec4 colDiffuse;      // colore albedo del material (maps[albedo].color)

out vec4 finalColor;

// Materiali unlit (glTF KHR_materials_unlit): il colore È la texture. L'illuminazione è
// già dipinta dentro dall'artista, quindi qui non c'è né BRDF né luci — sommarci quelle
// della scena raddoppierebbe le ombre.
//
// Niente conversione di gamma, e non è una dimenticanza: la texture è sRGB e il
// framebuffer pure, quindi il valore passa dritto. È lit.fs che deve linearizzare in
// ingresso, perché in mezzo ci fa dei conti.
void main()
{
    finalColor = texture(texture0, fragTexCoord)*colDiffuse*fragColor;
}
