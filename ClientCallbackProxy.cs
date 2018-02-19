using System;

namespace Starship.Injection {
    public class ClientCallbackProxy : MarshalByRefObject {

        public void Close() {
            if (SessionClosed != null) {
                SessionClosed();
            }
        }
        
        public static event Action SessionClosed;
    }
}