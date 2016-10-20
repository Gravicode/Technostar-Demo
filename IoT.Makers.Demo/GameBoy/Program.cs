using Gadgeteer.Modules.GHIElectronics;
using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;
using GHI.Glide;
using GHI.Glide.Display;
using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;

namespace GameBoy
{
    #region Forms
    public class Screen
    {
        public enum ScreenTypes { Splash = 0, MainMenu, XOX, Snake, Stars, Pong };
        public delegate void GoToFormEventHandler(ScreenTypes form, params string[] Param);
        public event GoToFormEventHandler FormRequestEvent;
        protected void CallFormRequestEvent(ScreenTypes form, params string[] Param)
        {
            // Event will be null if there are no subscribers
            if (FormRequestEvent != null)
            {
                FormRequestEvent(form, Param);
            }
        }
        protected GHI.Glide.Display.Window MainWindow { set; get; }
        public virtual void Init(params string[] Param)
        {
            //do nothing
        }

        public Screen(ref GHI.Glide.Display.Window window)
        {
            MainWindow = window;
        }
    }
    public class XOXForm : Screen
    {
        GHI.Glide.UI.Image ImgFull { set; get; }
        Gadgeteer.Modules.GHIElectronics.DisplayT35 displayTE35;
        Button button;
        bool GameIsOver = false;
        PlayerChips Turn = PlayerChips.O;
        PlayerChips Winner = PlayerChips.Blank;
        public enum PlayerChips { X, O, Blank }
        Hashtable Box { set; get; }
        Hashtable Control { set; get; }
        //imgFull
        public XOXForm(ref GHI.Glide.Display.Window window, ref Gadgeteer.Modules.GHIElectronics.DisplayT35 displayTE35, ref Button button)
            : base(ref window)
        {
            this.button = button;
            this.displayTE35 = displayTE35;
        }
        void Choose(int Pos)
        {
            if (GameIsOver) return;
            var box = (PlayerChips)Box[Pos];
            if (box == PlayerChips.Blank)
            {
                var img = (GHI.Glide.UI.Image)Control[Pos];
                Box[Pos] = Turn;
                if (Turn == PlayerChips.X)
                {
                    var tmp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.x), GT.Picture.PictureEncoding.JPEG);
                    img.Bitmap = tmp.MakeBitmap();
                    Turn = PlayerChips.O;
                }
                else
                {
                    var tmp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.o), GT.Picture.PictureEncoding.JPEG);
                    img.Bitmap = tmp.MakeBitmap();
                    Turn = PlayerChips.X;
                }
                img.Invalidate();
                if (CheckWin())
                {
                    GameIsOver = true;
                    //load game over
                    Bitmap bmp = null;
                    if (Winner == PlayerChips.X)
                    {
                        var tmp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.WIN), GT.Picture.PictureEncoding.JPEG);
                        bmp = tmp.MakeBitmap();
                    }
                    else if (Winner == PlayerChips.O)
                    {
                        var tmp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.LOSE), GT.Picture.PictureEncoding.JPEG);
                        bmp = tmp.MakeBitmap();
                    }
                    else
                    {
                        var tmp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.draw), GT.Picture.PictureEncoding.JPEG);
                        bmp = tmp.MakeBitmap();
                    }

                    ImgFull.Visible = true;
                    ImgFull.Bitmap = bmp;
                    ImgFull.Invalidate();

                    Thread.Sleep(3000);
                    CallFormRequestEvent(ScreenTypes.MainMenu);
                }
                else if (Turn == PlayerChips.O)
                {
                    Thread.Sleep(500);
                    ComMove();
                }
            }
        }

        bool EvaluatePos(PlayerChips player)
        {
            //ambil yg tinggal menang
            int BlankCounter = 0;
            int PlayerCounter = 0;
            int BlankPos = 0;
            //check horizontal
            for (int i = 1; i <= 7; i += 3)
            {
                BlankCounter = 0;
                PlayerCounter = 0;
                BlankPos = 0;
                for (int x = i; x <= i + 2; x++)
                {
                    if ((PlayerChips)Box[x] == player) PlayerCounter++;
                    if ((PlayerChips)Box[x] == PlayerChips.Blank)
                    {
                        BlankCounter++;
                        BlankPos = x;
                    }
                }
                if (BlankCounter == 1 && PlayerCounter == 2)
                {
                    Choose(BlankPos);
                    return true;
                }
            }
            //check vertikal
            for (int i = 1; i <= 3; i++)
            {
                BlankCounter = 0;
                PlayerCounter = 0;
                BlankPos = 0;

                for (int y = i; y <= i + 6; y += 3)
                {
                    if ((PlayerChips)Box[y] == player) PlayerCounter++;
                    if ((PlayerChips)Box[y] == PlayerChips.Blank)
                    {
                        BlankCounter++;
                        BlankPos = y;
                    }
                }
                if (BlankCounter == 1 && PlayerCounter == 2)
                {
                    Choose(BlankPos);
                    return true;
                }
            }
            //check diagonal

            {
                BlankCounter = 0;
                PlayerCounter = 0;
                BlankPos = 0;

                for (int y = 1; y <= 9; y += 4)
                {
                    if ((PlayerChips)Box[y] == player) PlayerCounter++;
                    if ((PlayerChips)Box[y] == PlayerChips.Blank)
                    {
                        BlankCounter++;
                        BlankPos = y;
                    }
                }
                if (BlankCounter == 1 && PlayerCounter == 2)
                {
                    Choose(BlankPos);
                    return true;
                }

            }
            {
                BlankCounter = 0;
                PlayerCounter = 0;
                BlankPos = 0;
                var tmp = (PlayerChips)Box[3];
                if (tmp != PlayerChips.Blank)
                {
                    for (int y = 3; y <= 7; y += 2)
                    {
                        if ((PlayerChips)Box[y] == player) PlayerCounter++;
                        if ((PlayerChips)Box[y] == PlayerChips.Blank)
                        {
                            BlankCounter++;
                            BlankPos = y;
                        }
                    }
                    if (BlankCounter == 1 && PlayerCounter == 2)
                    {
                        Choose(BlankPos);
                        return true;
                    }
                }
            }
            return false;
        }
        void ComMove()
        {
            //cek yang langsung menang
            if (EvaluatePos(PlayerChips.O)) return;
            //halangin mush yang mau menang
            if (EvaluatePos(PlayerChips.X)) return;

            //ambil tengah
            if ((PlayerChips)Box[5] == PlayerChips.Blank)
            {
                Choose(5);
                return;
            }
            //ambil sudut
            for (int i = 1; i <= 3; i += 2)
            {
                if ((PlayerChips)Box[i] == PlayerChips.Blank)
                {
                    Choose(i);
                    return;
                }
            }
            for (int i = 7; i <= 9; i += 2)
            {
                if ((PlayerChips)Box[i] == PlayerChips.Blank)
                {
                    Choose(i);
                    return;
                }
            }
            //acak
            for (int i = 1; i <= 9; i++)
            {
                if ((PlayerChips)Box[i] == PlayerChips.Blank)
                {
                    Choose(i);
                    return;
                }
            }
        }

        bool CheckWin()
        {
            int counter = 0;
            //check horizontal
            for (int i = 1; i <= 7; i += 3)
            {
                counter = 0;
                var tmp = (PlayerChips)Box[i];
                if (tmp == PlayerChips.Blank) break;
                for (int x = i; x <= i + 2; x++)
                {
                    if (tmp != (PlayerChips)Box[x]) break;
                    counter++;
                }
                if (counter >= 3)
                {
                    Winner = tmp;
                    return true;
                }
            }
            //check vertikal
            for (int i = 1; i <= 3; i++)
            {
                counter = 0;
                var tmp = (PlayerChips)Box[i];
                if (tmp == PlayerChips.Blank) break;
                for (int y = i; y <= i + 6; y += 3)
                {
                    if (tmp != (PlayerChips)Box[y]) break;
                    counter++;
                }
                if (counter >= 3)
                {
                    Winner = tmp;
                    return true;
                }
            }
            //check diagonal

            {
                counter = 0;
                var tmp = (PlayerChips)Box[1];
                if (tmp != PlayerChips.Blank)
                {
                    for (int y = 1; y <= 9; y += 4)
                    {
                        if (tmp != (PlayerChips)Box[y]) break;
                        counter++;
                    }
                    if (counter >= 3)
                    {
                        Winner = tmp;
                        return true;
                    }
                }
            }
            {
                counter = 0;
                var tmp = (PlayerChips)Box[3];
                if (tmp != PlayerChips.Blank)
                {
                    for (int y = 3; y <= 7; y += 2)
                    {
                        if (tmp != (PlayerChips)Box[y]) break;
                        counter++;
                    }
                    if (counter >= 3)
                    {
                        Winner = tmp;
                        return true;
                    }
                }
            }
            //check all
            counter = 0;
            for (int i = 1; i <= 9; i++)
            {
                if ((PlayerChips)Box[i] != PlayerChips.Blank)
                {
                    counter++;
                }
            }
            if (counter >= 9)
            {
                Winner = PlayerChips.Blank;
                return true;
            }
            return false;
        }
        public override void Init(params string[] Param)
        {
            button.ButtonPressed += BackHome;
            GameIsOver = false;
            Turn = PlayerChips.X;
            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.XOXForm));
            Control = new Hashtable();
            GT.Picture pic = null;
            Box = new Hashtable();
            ImgFull = (GHI.Glide.UI.Image)MainWindow.GetChildByName("imgFull");
            ImgFull.Visible = false;
            for (int i = 1; i <= 9; i++)
            {
                var imgTemp = (GHI.Glide.UI.Image)MainWindow.GetChildByName("box" + i);
                pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.blank), GT.Picture.PictureEncoding.JPEG);
                imgTemp.Bitmap = pic.MakeBitmap();
                Control.Add(i, imgTemp);
                Box.Add(i, PlayerChips.Blank);
                imgTemp.TapEvent += (x) =>
                {
                    if (Turn == PlayerChips.X)
                    {
                        var img = x as GHI.Glide.UI.Image;
                        var PinSel = int.Parse(img.Name.Substring(3));
                        Choose(PinSel);
                    }
                };
                if (i <= 2)
                {
                    var linehor = (GHI.Glide.UI.Image)MainWindow.GetChildByName("line" + i);
                    pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.linehor), GT.Picture.PictureEncoding.JPEG);
                    linehor.Bitmap = pic.MakeBitmap();
                }
                else if (i <= 4)
                {
                    var linever = (GHI.Glide.UI.Image)MainWindow.GetChildByName("line" + i);
                    pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.linever), GT.Picture.PictureEncoding.JPEG);
                    linever.Bitmap = pic.MakeBitmap();
                }

            }

            Glide.MainWindow = MainWindow;

            //MainWindow.Invalidate();
        }

        private void BackHome(Button sender, Button.ButtonState state)
        {

            button.ButtonPressed -= BackHome;
            CallFormRequestEvent(ScreenTypes.MainMenu);
        }
    }
    public class MainMenuForm : Screen
    {
        GHI.Glide.UI.Button BtnInbox { set; get; }

        public MainMenuForm(ref GHI.Glide.Display.Window window)
            : base(ref window)
        {

        }
        public void ChangeInboxCounter(int MessageCount)
        {
            if (MessageCount <= 0)
                BtnInbox.Text = "Message";
            else
                BtnInbox.Text = "Message (" + MessageCount + ")";
            BtnInbox.Invalidate();
        }
        public override void Init(params string[] Param)
        {

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.MenuForm));
            ArrayList control = new ArrayList();
            for (int i = 1; i < 5; i++)
            {
                var img = (GHI.Glide.UI.Image)MainWindow.GetChildByName("Img" + i);
                control.Add(img);
                GT.Picture pic = null;
                switch (i)
                {
                    case 1:
                        pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.xox), GT.Picture.PictureEncoding.JPEG);
                        img.Bitmap = pic.MakeBitmap();
                        break;
                    case 2:
                        pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.stars), GT.Picture.PictureEncoding.JPEG);
                        img.Bitmap = pic.MakeBitmap();
                        break;
                    case 3:
                        pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.snake), GT.Picture.PictureEncoding.JPEG);
                        img.Bitmap = pic.MakeBitmap();
                        break;
                    case 4:
                        pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.pong), GT.Picture.PictureEncoding.JPEG);
                        img.Bitmap = pic.MakeBitmap();
                        break;
                }
                var Btn = (GHI.Glide.UI.Button)MainWindow.GetChildByName("Btn" + i);
                if (i == 4)
                {
                    BtnInbox = Btn;
                }
                control.Add(Btn);
                Btn.PressEvent += (sender) =>
                {
                    var btn = sender as GHI.Glide.UI.Button;
                    switch (btn.Name)
                    {
                        case "Btn1":
                            CallFormRequestEvent(ScreenTypes.XOX);
                            break;
                        case "Btn2":
                            CallFormRequestEvent(ScreenTypes.Stars);
                            break;
                        case "Btn3":
                            CallFormRequestEvent(ScreenTypes.Snake);
                            break;
                        case "Btn4":
                            CallFormRequestEvent(ScreenTypes.Pong);
                            break;
                    }
                };
            }

            Glide.MainWindow = MainWindow;
            //MainWindow.Invalidate();
        }
    }
    public class StarsForm : Screen
    {
        public const int MaxStars = 50;
        public const int MaxHistory = 3;
        public const double PI2 = 6.283185307179586476925286766559;
        private Gadgeteer.Modules.GHIElectronics.Button button { set; get; }
        public static Random Rnd = new Random();
        GHI.Glide.UI.Image ImgStar { set; get; }
        public void StartStarsShow()
        {
            Bitmap bmp = new Bitmap(320, 240);
            Star[] stars = new Star[MaxStars];

            for (int i = 0; i < MaxStars; i++)
            {
                stars[i] = new Star();
            }

            while (true)
            {
                for (int i = 0; i < MaxStars; i++)
                {
                    var star = stars[i];

                    if (star.X < 0 || star.X > 320 || star.Y < 0 || star.Y > 240)
                    {
                        for (int j = 0; j <= MaxHistory; j++)
                        {
                            bmp.SetPixel(star.X - j * star.Dx, star.Y - j * star.Dy, Color.Black);
                        }

                        star.Initialize();
                    }
                    else
                    {
                        bmp.SetPixel(star.X, star.Y, Color.White);
                        star.X += star.Dx;
                        star.Y += star.Dy;

                        if (star.History < MaxHistory)
                        {
                            star.History++;
                        }
                        else
                        {
                            bmp.SetPixel(star.X - (MaxHistory + 1) * star.Dx, star.Y - (MaxHistory + 1) * star.Dy, Color.Black);
                        }
                    }
                }
                bmp.Flush();
                ImgStar.Bitmap = bmp;
                ImgStar.Invalidate();
                if (button.Pressed)
                {
                    break;
                }

            }
            CallFormRequestEvent(ScreenTypes.MainMenu);
        }


        public StarsForm(ref GHI.Glide.Display.Window window, ref Gadgeteer.Modules.GHIElectronics.Button button)
            : base(ref window)
        {
            this.button = button;
        }

        public override void Init(params string[] Param)
        {

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.UniversalForm));

            ImgStar = (GHI.Glide.UI.Image)MainWindow.GetChildByName("ImgMain");

            Glide.MainWindow = MainWindow;
            StartStarsShow();
        }
    }
    public class SplashForm : Screen
    {
        public SplashForm(ref GHI.Glide.Display.Window window)
            : base(ref window)
        {

        }
        public override void Init(params string[] Param)
        {

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.SplashForm));
            var img = (GHI.Glide.UI.Image)MainWindow.GetChildByName("ImgLogo");

            GT.Picture pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.logo), GT.Picture.PictureEncoding.JPEG);
            img.Bitmap = pic.MakeBitmap();

            Glide.MainWindow = MainWindow;
            //MainWindow.Invalidate();
            Thread.Sleep(2000);
            CallFormRequestEvent(ScreenTypes.MainMenu);

        }
    }
    public class PongForm : Screen
    {
        Joystick joystick { set; get; }
        Button button { set; get; }
        const int SpeedPad = 5;
        public class Arena
        {
            public const int Width = 320;
            public const int Height = 240;
        }
        public class Pad
        {
            public int Score { set; get; }
            public int X { set; get; }
            public int Y { set; get; }
            public const int Width = 100;
            public const int Height = 32;
            public Pad(int StartX, int StartY)
            {
                X = StartX;
                Y = StartY;
                Score = 0;
            }
            public void MovePad(int DX)
            {
                if (X - (Width / 2) + DX > 0 && DX < 0)
                {
                    X += DX;
                }
                else
                    if (X + (Width / 2) + DX <= Arena.Width && DX > 0)
                    {
                        X += DX;
                    }
            }
        }
        public class Ball
        {
            static Random rnd;
            public const int Width = 30;
            public const int Height = 30;
            public int Dx { set; get; }
            public int Dy { set; get; }
            public int X { set; get; }
            public int Y { set; get; }
            public void MoveBall(ref Pad pad1, ref Pad pad2)
            {
                //collision with pad
                if (Dy < 0 && X >= pad1.X - (Pad.Width / 2) && X <= pad1.X + (Pad.Width / 2) && Y - (Height / 2) <= pad1.Y + (Pad.Height / 2) && Y - (Height / 2) > pad1.Y - (Pad.Height / 2))
                {
                    //jika terkena pad 1 di atas
                    Dy *= -1;
                    if (X < pad1.X - (Pad.Width / 4))
                        Dx = -1 * (System.Math.Abs(X - (pad1.X - (Pad.Width / 4))) / 3);
                    else if (X > pad1.X + (Pad.Width / 4))
                        Dx = (System.Math.Abs(X - (pad1.X + (Pad.Width / 4))) / 3);
                }
                else if (Dy > 0 && X >= pad2.X - (Pad.Width / 2) && X <= pad2.X + (Pad.Width / 2) && Y + (Height / 2) >= pad2.Y - (Pad.Height / 2) && Y + (Height / 2) < pad2.Y + (Pad.Height / 2))
                {
                    //jika terkena pad 2 di bawah
                    Dy *= -1;
                    if (X < pad2.X - (Pad.Width / 4))
                        Dx = -1 * (System.Math.Abs(X - (pad2.X - (Pad.Width / 4))) / 3);
                    else if (X > pad2.X + (Pad.Width / 4))
                        Dx = (System.Math.Abs(X - (pad2.X + (Pad.Width / 4))) / 3);
                }
                //collision with wall
                if ((Dy < 0 && Y - (Height / 2) + Dy >= 0) || (Dy > 0 && Y + (Height / 2) + Dy <= Arena.Height))
                {
                    //nothing
                }
                else
                {
                    if (Dy < 0)
                        pad2.Score += 10;
                    else
                        pad1.Score += 10;
                    Dy *= -1;

                }
                if ((Dx < 0 && X - (Width / 2) + Dx >= 0) || (Dx > 0 && X + (Width / 2) + Dx <= Arena.Width))
                {
                    //nothing
                }
                else
                {
                    Dx *= -1;

                }
                Y += Dy;
                X += Dx;

            }
            public Ball()
            {
                if (rnd == null) rnd = new Random();
                X = Arena.Width / 2;
                Y = Arena.Height / 2;
                Dx = 4 + rnd.Next(5);
                Dy = 5;

            }
        }

        void ComMove(ref Ball ball, ref Pad MyPad)
        {
            if (System.Math.Abs(ball.X - MyPad.X) > 10)
            {
                if (ball.X > MyPad.X)
                {
                    MyPad.MovePad(SpeedPad);
                }
                else if (ball.X < MyPad.X)
                {
                    MyPad.MovePad(-SpeedPad);
                }
            }
        }
        GHI.Glide.UI.Image screen { set; get; }
        public PongForm(ref GHI.Glide.Display.Window window, ref Joystick joystick, ref Button button)
            : base(ref window)
        {
            this.joystick = joystick;
            this.button = button;
        }
        public override void Init(params string[] Param)
        {
            Bitmap bmp = new Bitmap(Arena.Width, Arena.Height);
            Pad player1 = new Pad(Arena.Width / 2, 16);
            Pad player2 = new Pad(Arena.Width / 2, 224);
            Ball ball = new Ball();
            //GT.Timer timer = new GT.Timer(50); // every second (1000ms)

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.UniversalForm));
            screen = (GHI.Glide.UI.Image)MainWindow.GetChildByName("ImgMain");

            Glide.MainWindow = MainWindow;
            //timer.Tick += (x) =>
            while (true)
            {
                if (button.Pressed)
                {
                    //timer.Stop();
                    break;
                }
                ball.MoveBall(ref player1, ref player2);
                ComMove(ref ball, ref player1);

                //control joystick
                var pos = joystick.GetPosition();
                if (pos.X < -0.3)
                {
                    player2.MovePad(-SpeedPad);
                }
                else if (pos.X > 0.3)
                {
                    player2.MovePad(SpeedPad);
                }
                //render

                bmp.DrawRectangle(Color.Black, 0, 0, 0, Arena.Width, Arena.Height, 0, 0, Color.Black, 0, 0, Color.Black, 0, 0, 100);
                //draw score
                bmp.DrawText("Player 1 :" + player1.Score, Resources.GetFont(Resources.FontResources.NinaB), GT.Color.Green, 5, (Arena.Height / 2) - 10);
                bmp.DrawText("Player 2 :" + player2.Score, Resources.GetFont(Resources.FontResources.NinaB), GT.Color.Blue, 5, (Arena.Height / 2) + 10);

                //draw pad 1
                bmp.DrawImage(player1.X - (Pad.Width / 2), player1.Y - (Pad.Height / 2), new Bitmap(Resources.GetBytes(Resources.BinaryResources.pad1), Bitmap.BitmapImageType.Jpeg), 0, 0, Pad.Width, Pad.Height);
                //draw pad 2
                bmp.DrawImage(player2.X - (Pad.Width / 2), player2.Y - (Pad.Height / 2), new Bitmap(Resources.GetBytes(Resources.BinaryResources.pad2), Bitmap.BitmapImageType.Jpeg), 0, 0, Pad.Width, Pad.Height);
                //draw ball
                bmp.DrawImage(ball.X - (Ball.Width / 2), ball.Y - (Ball.Height / 2), new Bitmap(Resources.GetBytes(Resources.BinaryResources.ball), Bitmap.BitmapImageType.Jpeg), 0, 0, Ball.Width, Ball.Height);
                bmp.Flush();
                screen.Bitmap = bmp;
                screen.Invalidate();
            }
            //timer.Start();
            CallFormRequestEvent(ScreenTypes.MainMenu);

        }


    }

    public class SnakeForm : Screen
    {
        public enum MoveDirection { Left, Up, Right, Down }
        Joystick joystick { set; get; }
        Button button { set; get; }

        GHI.Glide.UI.Image screen { set; get; }

        public class Position
        {
            public int Number { set; get; }
            public int X
            {
                set;
                get;
            }
            public int Y
            { set; get; }
        }

        public class SnakeNest
        {
            public const int Width = 320;
            public const int Height = 240;
            public const int BlockSize = 20;
            public const int MaxX = 16;
            public const int MaxY = 12;
            static Random Rnd;
            public int FoodRemaining { set; get; }
            public enum Things { Empty, Food, Snake }
            public Things[][] Block = new Things[MaxX][];
            public SnakeNest()
            {

                for (int x = 0; x < MaxX; x++)
                {
                    Block[x] = new Things[MaxY];
                    for (int y = 0; y < MaxY; y++)
                    {
                        Block[x][y] = Things.Empty;
                    }
                }
                if (Rnd == null) Rnd = new Random();
                FoodRemaining = 0;
                GenerateFoods();
            }

            public void GenerateFoods()
            {
                int Ax, Ay;
                FoodRemaining = 3 + Rnd.Next(10);
                for (int i = 0; i < FoodRemaining; i++)
                {
                    do
                    {
                        Ax = Rnd.Next(MaxX);
                        Ay = Rnd.Next(MaxY);

                    } while (Block[Ax][Ay] != Things.Empty);
                    Block[Ax][Ay] = Things.Food;
                }
            }
        }
        public class Snake : IEnumerable
        {
            public const int SnakeSpeed = 5;
            public int Score { set; get; }
            public bool IsDied { set; get; }
            private ArrayList Body;
            public MoveDirection Direction { set; get; }

            public int Count { get { return Body.Count; } }
            public Snake()
            {
                IsDied = false;
                Body = new ArrayList();
                for (int i = 0; i < 3; i++)
                {
                    Add(new Position() { Number = i + 1, X = (SnakeNest.MaxX / 2) + i, Y = SnakeNest.MaxY / 2 });
                }
                Direction = MoveDirection.Left;

            }
            public void SetPath(ref SnakeNest Nest, bool PutPath)
            {
                for (int i = 0; i < this.Body.Count; i++)
                {
                    if (!PutPath)
                        Nest.Block[this[i].X][this[i].Y] = SnakeNest.Things.Empty;
                    else
                        Nest.Block[this[i].X][this[i].Y] = SnakeNest.Things.Snake;
                }
            }
            public void Turn(MoveDirection NewDir)
            {
                switch (NewDir)
                {
                    case MoveDirection.Left:
                        if (Direction == MoveDirection.Right) return;
                        break;
                    case MoveDirection.Right:
                        if (Direction == MoveDirection.Left) return;
                        break;
                    case MoveDirection.Up:
                        if (Direction == MoveDirection.Down) return;
                        break;
                    case MoveDirection.Down:
                        if (Direction == MoveDirection.Up) return;
                        break;
                }

                Direction = NewDir;
            }
            public void Move(ref SnakeNest Nest)
            {
                Position Next = new Position();
                Next.X = this[0].X;
                Next.Y = this[0].Y;
                switch (Direction)
                {
                    case MoveDirection.Up:
                        if (this[0].Y == 0)
                        {
                            Next.Y = SnakeNest.MaxY - 1;
                        }
                        else
                        {
                            Next.Y--;
                        }
                        break;
                    case MoveDirection.Down:
                        if (this[0].Y >= SnakeNest.MaxY - 1)
                        {
                            Next.Y = 0;
                        }
                        else
                        {
                            Next.Y++;
                        }
                        break;
                    case MoveDirection.Left:
                        if (this[0].X <= 0)
                        {
                            Next.X = SnakeNest.MaxX - 1;
                        }
                        else
                        {
                            Next.X--;
                        }
                        break;
                    case MoveDirection.Right:
                        if (this[0].X >= SnakeNest.MaxX - 1)
                        {
                            Next.X = 0;
                        }
                        else
                        {
                            Next.X++;
                        }
                        break;
                }
                //check if die
                for (int z = 0; z < Body.Count; z++)
                {
                    if (Next.X == this[z].X && Next.Y == this[z].Y)
                    {
                        IsDied = true;
                        break;
                    }
                }
                if (IsDied)
                {
                    return;
                }
                else if (Nest.Block[Next.X][Next.Y] == SnakeNest.Things.Food)
                {
                    //check if food
                    Score += 10;
                    Nest.Block[Next.X][Next.Y] = SnakeNest.Things.Empty;
                    Add(new Position() { Number = Body.Count + 1, X = 99, Y = 99 });
                    Nest.FoodRemaining--;
                }
                //set track
                //Nest.Block[Next.X][Next.Y] = SnakeNest.Things.Snake;
                /*
                if (this[Body.Count - 1].X != 99)
                {
                    Nest.Block[this[Body.Count - 1].X][this[Body.Count - 1].Y] = SnakeNest.Things.Empty;
                }
                */
                //move body part
                for (int i = Body.Count - 1; i > 0; i--)
                {
                    this[i].X = this[i - 1].X;
                    this[i].Y = this[i - 1].Y;
                }
                //move head
                this[0].X = Next.X;
                this[0].Y = Next.Y;

            }
            public void Add(Position NewPart)
            {
                Body.Add(NewPart);
            }

            public void Remove(Position Part)
            {
                Body.Remove(Part);
            }

            public void RemoveAt(int index)
            {
                Body.RemoveAt(index);
            }

            public IEnumerator GetEnumerator()
            {
                return Body.GetEnumerator();
            }

            public Position this[int i]
            {
                get { return (Position)Body[i]; }
                set { Body[i] = value; }
            }
        }
        public SnakeForm(ref GHI.Glide.Display.Window window, ref Joystick joystick, ref Button button)
            : base(ref window)
        {
            this.joystick = joystick;
            this.button = button;
        }
        public override void Init(params string[] Param)
        {
            Bitmap bmp = new Bitmap(SnakeNest.Width, SnakeNest.Height);

            //GT.Timer timer = new GT.Timer(50); // every second (1000ms)
            //init for fist time
            Snake snake = new Snake();
            SnakeNest nest = new SnakeNest();

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.UniversalForm));
            screen = (GHI.Glide.UI.Image)MainWindow.GetChildByName("ImgMain");

            Glide.MainWindow = MainWindow;
            int DX = 0;
            int DY = 0;

            //timer.Tick += (x) =>
            while (true)
            {
                if (button.Pressed)
                {
                    //timer.Stop();
                    break;
                }

                //render 
                switch (snake.Direction)
                {
                    case MoveDirection.Left:
                        DX = -1;
                        DY = 0;
                        break;
                    case MoveDirection.Right:
                        DX = 1;
                        DY = 0;
                        break;
                    case MoveDirection.Up:
                        DY = -1;
                        DX = 0;
                        break;
                    case MoveDirection.Down:
                        DY = 1;
                        DX = 0;
                        break;
                }

                for (int i = 0; i < SnakeNest.BlockSize; i += Snake.SnakeSpeed)
                {
                    bmp.Clear();
                    bmp.DrawRectangle(Color.Black, 0, 0, 0, SnakeNest.Width, SnakeNest.Height, 0, 0, Color.Black, 0, 0, Color.Black, 0, 0, 100);
                    //draw score
                    bmp.DrawText("Score :" + snake.Score, Resources.GetFont(Resources.FontResources.NinaB), GT.Color.Yellow, 5, 5);
                    //food remaining
                    bmp.DrawText("Food remaining :" + nest.FoodRemaining, Resources.GetFont(Resources.FontResources.NinaB), GT.Color.Yellow, 5, 18);
                    //draw food
                    for (int x = 0; x < SnakeNest.MaxX; x++)
                        for (int y = 0; y < SnakeNest.MaxY; y++)
                        {
                            if (nest.Block[x][y] == SnakeNest.Things.Food)
                            {
                                bmp.DrawImage(x * SnakeNest.BlockSize, y * SnakeNest.BlockSize, new Bitmap(Resources.GetBytes(Resources.BinaryResources.food), Bitmap.BitmapImageType.Jpeg), 0, 0, SnakeNest.BlockSize, SnakeNest.BlockSize);
                            }
                        }

                    //draw snake movement

                    for (int z = 0; z < snake.Count; z++)
                    {
                        if (z <= 0)
                        {
                            bmp.DrawImage((snake[z].X * SnakeNest.BlockSize) + (DX * i), (snake[z].Y * SnakeNest.BlockSize) + (DY * i), new Bitmap(Resources.GetBytes(Resources.BinaryResources.snakebody), Bitmap.BitmapImageType.Jpeg), 0, 0, SnakeNest.BlockSize, SnakeNest.BlockSize);
                        }
                        else
                        {
                            int OX = 0;
                            int OY = 0;
                            if (snake[z].X > snake[z - 1].X)
                            {
                                OX = -1;
                            }
                            else if (snake[z].X < snake[z - 1].X)
                            {
                                OX = 1;
                            }
                            else
                                if (snake[z].Y > snake[z - 1].Y)
                                {
                                    OY = -1;
                                }
                                else
                                    if (snake[z].Y < snake[z - 1].Y)
                                    {
                                        OY = 1;
                                    }
                            bmp.DrawImage((snake[z].X * SnakeNest.BlockSize) + (OX * i), (snake[z].Y * SnakeNest.BlockSize) + (OY * i), new Bitmap(Resources.GetBytes(Resources.BinaryResources.snakebody), Bitmap.BitmapImageType.Jpeg), 0, 0, SnakeNest.BlockSize, SnakeNest.BlockSize);

                        }

                    }

                    bmp.Flush();
                    screen.Bitmap = bmp;
                    screen.Invalidate();
                }

                snake.Move(ref nest);
                if (snake.IsDied)
                {
                    var tmp = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.die), GT.Picture.PictureEncoding.JPEG);
                    bmp = tmp.MakeBitmap();
                    screen.Bitmap = bmp;
                    screen.Invalidate();
                    Thread.Sleep(3000);
                    break;
                }
                if (nest.FoodRemaining <= 0)
                {
                    nest.GenerateFoods();
                }
                //control joystick
                var pos = joystick.GetPosition();
                if (pos.X < -0.3)
                {
                    snake.Turn(MoveDirection.Left);
                }
                else if (pos.X > 0.3)
                {
                    snake.Turn(MoveDirection.Right);

                }
                else if (pos.Y > 0.3)
                {
                    snake.Turn(MoveDirection.Up);

                }
                else if (pos.Y < -0.3)
                {
                    snake.Turn(MoveDirection.Down);
                }
            }
            //timer.Start();
            CallFormRequestEvent(ScreenTypes.MainMenu);

        }


    }
    #endregion

    #region Stars
    class Star
    {
        public int X;
        public int Y;
        public int Dx;
        public int Dy;
        public int History;

        public Star()
        {
            Initialize();
        }

        public void Initialize()
        {
            do
            {
                var angle = StarsForm.PI2 * StarsForm.Rnd.NextDouble();
                var speed = StarsForm.Rnd.Next(5) + 1;

                Dx = (int)System.Math.Round(System.Math.Sin(angle) * speed);
                Dy = (int)System.Math.Round(System.Math.Cos(angle) * speed);
            } while (Dx == 0 || Dy == 0);

            X = 8 * Dx + 160;
            Y = 8 * Dy + 120;
            History = 0;
        }
    }
    #endregion

    public class Display
    {
        public static Gadgeteer.Modules.GHIElectronics.DisplayT35 displayT35 { set; get; }

        public static void Clear()
        {
            displayT35.SimpleGraphics.Clear();
        }
        /// <summary>
        /// Draws a string to the display.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="x">The x coordinate where to draw.</param>
        /// <param name="y">The y coordinate where to draw.</param>
        /// <param name="font">The font to use.</param>
        /// <param name="foreColor">The color of the text.</param>
        /// <param name="backColor">The background color of the text.</param>
        public static void Draw(string text, int x, int y, Font font, Color foreColor, Color backColor)
        {
            int width = 0, height;
            font.ComputeExtent(text, out width, out height);

            Bitmap bmp = new Bitmap(width, height);
            bmp.DrawRectangle(backColor, 100, 0, 0, width, height, 0, 0, backColor, 0, 0, backColor, 0, 0, 0);
            bmp.DrawText(text, font, foreColor, 0, 0);

            Draw(bmp, x, y);
        }

        /// <summary>
        /// Draws a bitmap to the display.
        /// </summary>
        /// <param name="image">The bitmap to display.</param>
        /// <param name="x">The x coordinate where to draw.</param>
        /// <param name="y">The y coordinate where to draw.</param>
        public static void Draw(Bitmap image, int x, int y)
        {
            byte[] data = new byte[image.Width * image.Height * 2];
            GHI.Utilities.Bitmaps.Convert(image, GHI.Utilities.Bitmaps.Format.Bpp16BgrLe, data);
            //Bitmap img = new Bitmap(data,Bitmap.BitmapImageType.Bmp);
            displayT35.SimpleGraphics.DisplayImage(image, (ushort)x, (ushort)y);
        }
    }
    public partial class Program
    {
        private static GHI.Glide.Display.Window MainWindow;
        private static Screen.ScreenTypes ActiveWindow { set; get; }
        private static Screen ActiveForm = null;
        Hashtable Screens { set; get; }

        #region Tunes
        void PlayMusic()
        {
            var melody = new Gadgeteer.Modules.GHIElectronics.Tunes.Melody();
            Tunes.MusicNote note = new Tunes.MusicNote(Tunes.Tone.C4, 400);

            melody.Add(note);

            // up
            melody.Add(PlayNote(Tunes.Tone.C4));
            melody.Add(PlayNote(Tunes.Tone.D4));
            melody.Add(PlayNote(Tunes.Tone.E4));
            melody.Add(PlayNote(Tunes.Tone.F4));
            melody.Add(PlayNote(Tunes.Tone.G4));
            melody.Add(PlayNote(Tunes.Tone.A4));
            melody.Add(PlayNote(Tunes.Tone.B4));
            melody.Add(PlayNote(Tunes.Tone.C5));

            //// back down
            melody.Add(PlayNote(Tunes.Tone.B4));
            melody.Add(PlayNote(Tunes.Tone.A4));
            melody.Add(PlayNote(Tunes.Tone.G4));
            melody.Add(PlayNote(Tunes.Tone.F4));
            melody.Add(PlayNote(Tunes.Tone.E4));
            melody.Add(PlayNote(Tunes.Tone.D4));
            melody.Add(PlayNote(Tunes.Tone.C4));

            //// arpeggio
            melody.Add(PlayNote(Tunes.Tone.E4));
            melody.Add(PlayNote(Tunes.Tone.G4));
            melody.Add(PlayNote(Tunes.Tone.C5));
            melody.Add(PlayNote(Tunes.Tone.G4));
            melody.Add(PlayNote(Tunes.Tone.E4));
            melody.Add(PlayNote(Tunes.Tone.C4));

            //tunes.Play();

            //Thread.Sleep(100);

            melody.Add(PlayNote(Tunes.Tone.E4));
            melody.Add(PlayNote(Tunes.Tone.G4));
            melody.Add(PlayNote(Tunes.Tone.C5));
            melody.Add(PlayNote(Tunes.Tone.G4));
            melody.Add(PlayNote(Tunes.Tone.E4));
            melody.Add(PlayNote(Tunes.Tone.C4));

            tunes.Play(melody);

        }
        Tunes.MusicNote PlayNote(Tunes.Tone tone)
        {
            Tunes.MusicNote note = new Tunes.MusicNote(tone, 200);

            return note;
        }
        #endregion

        #region Stars


        private void button_ButtonPressed(Button sender, Button.ButtonState state)
        {
            throw new NotImplementedException();
        }
        #endregion

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            /*******************************************************************************************
            Modules added in the Program.gadgeteer designer view are used by typing 
            their name followed by a period, e.g.  button.  or  camera.
            
            Many modules generate useful events. Type +=<tab><tab> to add a handler to an event, e.g.:
                button.ButtonPressed +=<tab><tab>
            
            If you want to do something periodically, use a GT.Timer and handle its Tick event, e.g.:
                GT.Timer timer = new GT.Timer(1000); // every second (1000ms)
                timer.Tick +=<tab><tab>
                timer.Start();
            *******************************************************************************************/


            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");
            /*
            Screens = new Hashtable();
            //populate all form
            var F1 = new SplashForm(ref MainWindow);
            F1.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Splash, F1);

            var F2 = new MainMenuForm(ref MainWindow);
            F2.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.MainMenu, F2);

            var F3 = new XOXForm(ref MainWindow, ref displayT35, ref button);
            F3.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.XOX, F3);

            var F4 = new StarsForm(ref MainWindow, ref button);
            F4.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Stars, F4);

            var F5 = new PongForm(ref MainWindow, ref joystick, ref button);
            F5.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Pong, F5);

            var F6 = new SnakeForm(ref MainWindow, ref joystick, ref button);
            F6.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Snake, F6);
            */
            Glide.FitToScreen = true;
            GlideTouch.Initialize();

            //load splash
            LoadForm(Screen.ScreenTypes.Splash);
            PlayMusic();
            new Thread(() =>
            {
                int pct = 0;
                while (true)
                {
                    ledStrip.SetLeds(pct);
                    pct++;
                    if (pct > 7) pct = 0;
                    Thread.Sleep(200);
                }
            }).Start();
        }

        void LoadForm(Screen.ScreenTypes form, params string[] Param)
        {
            ActiveWindow = form;

            switch (form)
            {
                case Screen.ScreenTypes.Splash:
                    ActiveForm = new SplashForm(ref MainWindow);
                    ActiveForm.FormRequestEvent += General_FormRequestEvent;
                    break;
                case Screen.ScreenTypes.MainMenu:
                    ActiveForm = new MainMenuForm(ref MainWindow);;
                    ActiveForm.FormRequestEvent += General_FormRequestEvent;
                    break;
                case Screen.ScreenTypes.XOX:
                    ActiveForm = new XOXForm(ref MainWindow, ref displayT35, ref button);
                    ActiveForm.FormRequestEvent += General_FormRequestEvent;
                    break;
                case Screen.ScreenTypes.Stars:
                    ActiveForm = new StarsForm(ref MainWindow, ref button);
                    ActiveForm.FormRequestEvent += General_FormRequestEvent;
                    break;
                case Screen.ScreenTypes.Pong:
                    ActiveForm = new PongForm(ref MainWindow, ref joystick, ref button);
                    ActiveForm.FormRequestEvent += General_FormRequestEvent;
                    break;
                case Screen.ScreenTypes.Snake:
                    ActiveForm = new SnakeForm(ref MainWindow, ref joystick, ref button);
                    ActiveForm.FormRequestEvent += General_FormRequestEvent;
                    break;

                default:
                    return;
                //throw new Exception("Belum diterapkan");
            }
            ActiveForm.Init(Param);
            //clear unused memory
            Debug.GC(true);
        }
        void General_FormRequestEvent(Screen.ScreenTypes form, params string[] Param)
        {
            LoadForm(form, Param);
        }


    }
}
