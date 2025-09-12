using NewInputByReference.Bindings;

namespace NewInputByReference.EditorExtensions
{
    internal class NumpadLayout : Layout
    {
        public NumpadLayout()
        {
            var keysList = new[]
            {
                // 0
                "Up Arrow",
            
                // 1 -> 2
                "Left Arrow", "Right Arrow",
            
                // 3
                "Down Arrow"
            };
            
            Initialize(keysList, KeyType.Keyboard);
        }
        
        public override void DrawLayout()
        {
            // Up Arrow
            DrawKeyLine(0, 0, 700);
            
            //Left Arrow -> Right Arrow 
            DrawKeyLine(1, 2, 450);
            
            // Down Arrow
            DrawKeyLine(3, 3, 700);
        }
    }
}