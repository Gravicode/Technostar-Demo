//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GameBoy
{
    
    internal partial class Resources
    {
        private static System.Resources.ResourceManager manager;
        internal static System.Resources.ResourceManager ResourceManager
        {
            get
            {
                if ((Resources.manager == null))
                {
                    Resources.manager = new System.Resources.ResourceManager("GameBoy.Resources", typeof(Resources).Assembly);
                }
                return Resources.manager;
            }
        }
        internal static Microsoft.SPOT.Font GetFont(Resources.FontResources id)
        {
            return ((Microsoft.SPOT.Font)(Microsoft.SPOT.ResourceUtility.GetObject(ResourceManager, id)));
        }
        internal static string GetString(Resources.StringResources id)
        {
            return ((string)(Microsoft.SPOT.ResourceUtility.GetObject(ResourceManager, id)));
        }
        internal static byte[] GetBytes(Resources.BinaryResources id)
        {
            return ((byte[])(Microsoft.SPOT.ResourceUtility.GetObject(ResourceManager, id)));
        }
        [System.SerializableAttribute()]
        internal enum StringResources : short
        {
            SplashForm = -26253,
            MenuForm = -18109,
            UniversalForm = -16838,
            XOXForm = 12336,
        }
        [System.SerializableAttribute()]
        internal enum BinaryResources : short
        {
            LOSE = -14382,
            linehor = -13078,
            xox = -11430,
            food = -9277,
            blank = -4503,
            pong = -764,
            draw = 11545,
            WIN = 13352,
            snakebody = 15831,
            pad2 = 16693,
            logo = 17715,
            linever = 21274,
            die = 21662,
            snake = 24740,
            stars = 28369,
            pad1 = 29035,
            o = 31241,
            x = 31260,
            ball = 31292,
        }
        [System.SerializableAttribute()]
        internal enum FontResources : short
        {
            small = 13070,
            NinaB = 18060,
        }
    }
}
