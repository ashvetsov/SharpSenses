using System;
using System.Linq;

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

        private FacialExpression[] _facialExpressions;
        public FacialExpression[] FacialExpressions {
            get { return _facialExpressions ?? new FacialExpression[0]; }
            set {
                if (_facialExpressions == value) return;

                var old = _facialExpressions;
                _facialExpressions = value;
                foreach (var expression in _facialExpressions ?? new FacialExpression[0]) {
                    if (!old.Contains(expression)) OnFacialExpressionBegin(expression);
                }
                foreach (var expression in old ?? new FacialExpression[0]) {
                    if (!_facialExpressions.Contains(expression)) OnFacialExpressionEnd(expression);
                }
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

        public event EventHandler<FacialExpressionEventArgs> FacialExpressionBegin;
        public event EventHandler<FacialExpressionEventArgs> FacialExpressionEnd;

        protected virtual void OnFacialExpressionBegin(FacialExpression expression) {
            var handler = FacialExpressionBegin;
            if (handler != null) handler(this, new FacialExpressionEventArgs(expression));
        }
        protected virtual void OnFacialExpressionEnd(FacialExpression expression) {
            var handler = FacialExpressionEnd;
            if (handler != null) handler(this, new FacialExpressionEventArgs(expression));
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

    public class FacialExpressionEventArgs : EventArgs {
        public FacialExpression FacialExpression { get; set; }

        public FacialExpressionEventArgs(FacialExpression expression) {
            FacialExpression = expression;
        }
    }
}