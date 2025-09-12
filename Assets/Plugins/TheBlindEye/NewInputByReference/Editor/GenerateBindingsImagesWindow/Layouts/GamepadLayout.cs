using NewInputByReference.Bindings;

namespace NewInputByReference.EditorExtensions
{
    internal class GamepadLayout : Layout
    {
        public GamepadLayout()
        {
            var keysList = new[]
            {
                // 0 -> 3
                "LT", "LB", "RT", "RB",
                
                // 4 -> 5
                "LS/Up", "Y", 
                
                // 6 -> 12
                "LS/Left", "Left Stick Press", "LS/Right", "Select", "Start", "X", "B",
                
                // 13 -> 16
                "D-Pad/Up", "LS/Down", "A", "RS/Up",
                
                // 17 -> 21
                "D-Pad/Left", "D-Pad/Right", "RS/Left", "Right Stick Press", "RS/Right",
                
                // 22 -> 26
                "D-Pad/Down", "D-Pad", "LS", "RS", "RS/Down"
            };
            
            Initialize(keysList, KeyType.Gamepad);
        }
        
        public override void DrawLayout()
        {
            // LT -> RB 
            DrawKeyLine(0, 3, 200, new []{0, -250, 800, -60});
            
            // LS/Up -> Y
            DrawKeyLine(4, 5, 350, new []{50, 250});
            
            // LS/Left -> B
            DrawKeyLine(6, 12, 150, new []{150,-120,-180,-150,-230,-180,-170});
            
            // D-Pad/Up -> RS/Up
            DrawKeyLine(13, 16, 120, new []{45, 20, 470, 20});
            
            // D-Pad/Left -> RS/Right
            DrawKeyLine(17, 21, 90, new []{-10, 50, 840, -10, -30});
            
            // D-Pad/Down -> RS/Down
            DrawKeyLine(22, 26, 65, new[]{100, 270, -100, -150, 170});
        }
    }
}