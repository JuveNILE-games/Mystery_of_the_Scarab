using NewInputByReference.Bindings;

namespace NewInputByReference.EditorExtensions
{
    internal class KeyboardLayout : Layout
    {
        public KeyboardLayout()
        {
            var keysList = new[]
            {
                // 0 -> 11
                "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", 
                "F9", "F10", "F11", "F12", 
                
                // 12 -> 25
                "`", "1", "2", "3", "4", "5", "6",
                "7", "8", "9", "0", "-", "=", "Backspace", 
                
                // 26 -> 38
                "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O",
                "P", "[", "]", 
                
                // 39 -> 51
                "Caps Lock", "A", "S", "D", "F", "G", "H", "J", "K", "L", 
                ";", "'", "Enter", 
                
                // 52 -> 62
                "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", 
                ".", "/", 
                
                // 63 -> 65
                "Control", "Space", "Alt"
            };

            Initialize(keysList, KeyType.Keyboard);
        }
      

        public override void DrawLayout()
        {
            // F1 -> F12
            DrawKeyLine(0, 11, 50);
            
            // ~ -> BackSpace
            DrawKeyLine(12, 25, 60);
            
            // Tab -> ]
            DrawKeyLine(26, 38, 40);
  
            // Caps -> Enter
            DrawKeyLine(39, 51, 40);
            
            // Shift -> /
            DrawKeyLine(52, 62, 55);
            
            // Ctrl -> Alt 
            DrawKeyLine(63, 65, 307);
        }
    }
}