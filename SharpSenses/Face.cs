using System;

namespace SharpSenses {
    public class Face : Item {
        private FacialEmotion _facialEmotion;
        public Mouth Mouth { get; private set; }

        public FacialEmotion FacialEmotion {
            get { return _facialEmotion; }
            set {
                if (_facialEmotion == value) {
                    return;
                }
                var old = _facialEmotion;
                _facialEmotion = value;
                RaisePropertyChanged(() => FacialEmotion);
                OnFacialEmotionChanged(old, value);
            }
        }

        public event EventHandler<FacialEmotionEventArgs> FacialEmotionChanged;

        public Face() {
            Mouth = new Mouth();
        }
        protected virtual void OnFacialEmotionChanged(FacialEmotion old, FacialEmotion @new) {
            var handler = FacialEmotionChanged;
            if (handler != null) handler(this, new FacialEmotionEventArgs(old, @new));
        }
    }

    public class FacialEmotionEventArgs : EventArgs {
        public FacialEmotion OldFacialEmotion { get; set; }
        public FacialEmotion NewFacialEmotion { get; set; }

        public FacialEmotionEventArgs(FacialEmotion oldFacialEmotion, FacialEmotion newFacialEmotion) {
            OldFacialEmotion = oldFacialEmotion;
            NewFacialEmotion = newFacialEmotion;
        }
    }
}