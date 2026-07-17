#version 330

// Attributi e uniform standard forniti automaticamente da raylib.
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;

uniform mat4 mvp;

out vec2 fragTexCoord;
out vec4 fragColor;

// Niente fragPosition/fragNormal e niente matModel/matNormal: senza luci non servono a
// nessuno. È l'unica differenza rispetto a lit.vs, ma è il motivo per cui questo file
// esiste invece di riusare quello — un varying che nessuno legge è una domanda in più
// per chi legge lo shader.
void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;

    gl_Position = mvp*vec4(vertexPosition, 1.0);
}
