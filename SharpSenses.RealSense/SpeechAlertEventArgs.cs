using System;

namespace SharpSenses.RealSense {
    public class SpeechAlertEventArgs : EventArgs {
        public PXCMSpeechRecognition.AlertType AlertType { get; set; }

        public SpeechAlertEventArgs(PXCMSpeechRecognition.AlertType type) {
            AlertType = type;
        }
    }
}