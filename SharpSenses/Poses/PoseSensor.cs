using System;

namespace SharpSenses.Poses {
    public class PoseSensor : IPoseSensor {
        public event EventHandler<HandPoseEventArgs> PeaceBegin;
        public event EventHandler<HandPoseEventArgs> PeaceEnd;

        public void OnPosePeaceBegin(Hand hand)
        {
            var handler = PeaceBegin;
            if (handler != null) handler(this, new HandPoseEventArgs(hand));
        }

        public void OnPosePeaceEnd(Hand hand)
        {
            var handler = PeaceEnd;
            if (handler != null) handler(this, new HandPoseEventArgs(hand));
        }

        /*
         * NOTE: These events are an awful hack.
         * Especially the place where they are initiated.
         * 
         * This is done because SharpSenses implementation requires
         * hand order, but ignoring it allows us to detect hands
         * earlier. In face - we don't need hands order at all.
         */
        public event EventHandler<HandPoseEventArgs> HandOverFaceBegin;
        public event EventHandler<HandPoseEventArgs> HandOverFaceEnd;

        public void OnHandOverFaceBegin()
        {
            var handler = HandOverFaceBegin;
            if (handler != null) handler(this, new HandPoseEventArgs(null));

            Console.WriteLine("Pose: HandOverFaceBegin()");
        }

        public void OnHandOverFaceEnd()
        {
            var handler = HandOverFaceEnd;
            if (handler != null) handler(this, new HandPoseEventArgs(null));

            Console.WriteLine("Pose: HandOverFaceEnd()");
        }
    }
}