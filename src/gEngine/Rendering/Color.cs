namespace gEngine.Rendering;

    public readonly struct Color
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public Color(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        // Costanti statiche comuni
        public static readonly Color White = new Color(255, 255, 255, 255);
        public static readonly Color Black = new Color(0, 0, 0, 255);
        public static readonly Color Red = new Color(255, 0, 0, 255);
        public static readonly Color Gray = new Color(128, 128, 128, 255);
        public static readonly Color DarkGray = new Color(64, 64, 64, 255);
        public static readonly Color LightGray = new Color(200, 200, 200, 255);

        // Override utili
        public override string ToString()
        {
            return $"Color({R}, {G}, {B}, {A})";
        }

        // Uguaglianza
        public override bool Equals(object? obj)
        {
            return obj is Color other &&
                   R == other.R &&
                   G == other.G &&
                   B == other.B &&
                   A == other.A;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(R, G, B, A);
        }

        public static bool operator ==(Color left, Color right) => left.Equals(right);
        public static bool operator !=(Color left, Color right) => !left.Equals(right);
    }
