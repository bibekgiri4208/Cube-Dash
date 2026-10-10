namespace CubeDash
{
    /// <summary>Pairs distinct press edges; a third press starts a new pair rather than toggling twice.</summary>
    public sealed class DoublePressInput
    {
        private bool armed;
        private double lastPress;

        public bool Press(double now, double window = 0.35)
        {
            bool paired = armed && now >= lastPress && now - lastPress <= window;
            armed = !paired;
            lastPress = now;
            return paired;
        }

        public void Reset() { armed = false; lastPress = 0; }
    }
}
