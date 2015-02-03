namespace SharpSenses.Gestures {
    public class GestureSlideRight : GestureSlide {
        public GestureSlideRight(Hand hand, int middle) : base(hand, middle) {}

        protected override double GetBeginLimit() {
            return Middle + GestureLength;
        }

        protected override double GetEndLimit() {
            return Middle - GestureLength;
        }

        protected override bool IsRightDirection(double currentPrimaryValue, double lastPrimaryValue) {
            return currentPrimaryValue - WrongDirectionTolerance <= lastPrimaryValue;
        }

        protected override bool IsInEndArea(double currentPrimaryValue, double endLimit) {
            return currentPrimaryValue <= endLimit;
        }

        protected override  bool IsInStartArea(double currentPrimaryValue, double beginLimit) {
            return currentPrimaryValue >= beginLimit;
        }

        protected override double GetLastPrimaryValue(Position lastPosition) {
            return lastPosition.Image.X;
        }

        protected override  double GetCurrentSecundaryValue(Position position) {
            var positionY = position.Image.Y;
            return positionY;
        }

        protected override  double GetCurrentPrimaryValue(Position position) {
            return position.Image.X;
        }
    }
}