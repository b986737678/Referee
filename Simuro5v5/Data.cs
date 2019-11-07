﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Referee.Simuro5v5
{
    public enum Side
    {
        Nobody,
        Yellow,
        Blue,
    };

    public enum MatchPhase
    {
        FirstHalf,
        SecondHalf,
        OverTime,
        Penalty
    }

    /// <summary>
    /// 单拍信息
    /// 包括：机器人和球的信息；比分；时间；比赛阶段（上下半场等）；裁判（包括本拍的判决信息）
    /// 这个类的目标是用来表示完整的一拍信息，属于单拍的信息都应该放在这里，由于平台比赛由拍驱动，所以这个类在平台内大量使用。
    /// </summary>
    public class MatchInfo : ICloneable
    {
        // TODO 将这三者提取出来作为另外一个类EntityInfo，因为有许多地方只需要物体的信息，但是仍传递了整个MatchInfo
        public Robot[] BlueRobots { get; set; }
        public Robot[] YellowRobots { get; set; }
        public Ball Ball;

        // 时间
        public int TickMatch;
        public int TickPhase;
        // 比分
        public MatchScore Score;
        // 比赛阶段：上下半场、加时、点球大战
        public MatchPhase MatchPhase;

        // 当前拍的裁判信息。含有裁判在*本拍*的一些状态
        public Referee Referee;

        public MatchInfo()
        {
            BlueRobots = new Robot[Const.RobotsPerTeam];
            YellowRobots = new Robot[Const.RobotsPerTeam];
            Referee = new Referee();
        }

        public MatchInfo(Robot[] blue, Robot[] yellow, Ball ball)
        {
            BlueRobots = (Robot[])blue.Clone();
            YellowRobots = (Robot[])yellow.Clone();
            Ball = ball;
        }

        /// <summary>
        /// 将两个摆位信息拼接成一个MatchInfo
        /// </summary>
        /// <param name="blue">蓝方摆位信息</param>
        /// <param name="yellow">黄方摆位信息</param>
        /// <param name="whosball">球的信息来自哪方</param>
        public MatchInfo(PlacementInfo blue, PlacementInfo yellow, Side whosball)
        {
            BlueRobots = (Robot[])blue.Robots.Clone();
            YellowRobots = (Robot[])yellow.Robots.Clone();
            switch (whosball)
            {
                case Side.Blue:
                    Ball = blue.Ball;
                    break;
                case Side.Yellow:
                    Ball = yellow.Ball;
                    break;
                default:
                    //假设是Nobaby，黄蓝方球的坐标是一样
                    Ball = blue.Ball;
                    break;
            }
        }

        public object Clone()
        {
            return new MatchInfo()
            {
                Ball = Ball,
                TickMatch = TickMatch,
                TickPhase = TickPhase,
                MatchPhase = MatchPhase,
                Score = Score,
                Referee = (Referee)Referee.Clone(),
                BlueRobots = (Robot[])BlueRobots.Clone(),
                YellowRobots = (Robot[])YellowRobots.Clone(),
            };
        }

        public static MatchInfo NewDefaultPreset()
        {
            var info = new MatchInfo();
            info.Ball.moveTo(0, 0);
            info.Ball.angularVelocity = 0;
            info.Ball.linearVelocity = Vector2D.Zero;
            //x, y, rotation
            var yellowData = new[,]
            {
                {-102.5d, 0, 90}, {-81.2d, 48, 0}, {-81.2d, -48, 0}, {-29.8d, 48, 0}, {-29.8d, -48, 0}
            };
            var blueData = new[,]
            {
                {102.5d, 0, -90}, {81.2d, -48, 180}, {81.2d, 48, 180}, {29.8d, -48, 180}, {29.8d, 48, 180}
            };
            Robot InitRobot(Robot rb, double[,] data, int elem)
            {
                rb.pos.x = data[elem, 0];
                rb.pos.y = data[elem, 1];
                rb.rotation = data[elem, 2];
                rb.wheel.left = rb.wheel.right = 0;
                rb.linearVelocity = Vector2D.Zero;
                return rb;
            }
            Robot[] InitMe(IEnumerable<Robot> rbs, double[,] data)
            {
                return rbs.Select((rb, i) => InitRobot(rb, data, i)).ToArray();
            }
            info.YellowRobots = InitMe(info.YellowRobots, yellowData);
            info.BlueRobots = InitMe(info.BlueRobots, blueData);

            return info;
        }

        /// <summary>
        /// 更新MatchInfo中的所有信息
        /// </summary>
        /// <param name="matchInfo"></param>
        public void UpdateFrom(MatchInfo matchInfo)
        {
            BlueRobots = (Robot[])matchInfo.BlueRobots.Clone();
            YellowRobots = (Robot[])matchInfo.YellowRobots.Clone();
            Ball = matchInfo.Ball;

            Score = matchInfo.Score;
            TickMatch = matchInfo.TickMatch;
            MatchPhase = matchInfo.MatchPhase;
            Referee = (Referee)matchInfo.Referee.Clone();
        }

        /// <summary>
        /// 更新实体的状态，包括黄蓝方机器人和球
        /// </summary>
        /// <param name="blue"></param>
        /// <param name="yellow"></param>
        /// <param name="ball"></param>
        public void UpdateFrom(Robot[] blue, Robot[] yellow, Ball ball)
        {
            BlueRobots = (Robot[])blue.Clone();
            YellowRobots = (Robot[])yellow.Clone();
            Ball = ball;
        }

        public void UpdateFrom(Robot[] robots, Side side)
        {
            switch (side)
            {
                case Side.Blue:
                    BlueRobots = (Robot[])robots.Clone();
                    break;
                case Side.Yellow:
                    YellowRobots = (Robot[])robots.Clone();
                    break;
            }
        }

        public void UpdateFrom(Ball ball)
        {
            Ball = ball;
        }
        
        /// <summary>
        /// 满足右攻假设，获取双方在*蓝方*视角下的SideInfo
        /// 首先根据当前信息构造SideInfo，然后如果需要的是黄方数据，为了满足右攻假设，进行视角的转换
        /// </summary>
        /// <param name="side"></param>
        /// <returns></returns>
        public SideInfo GetSide(Side side)
        {
            SideInfo si = new SideInfo
            {
                currentBall = Ball,
            };
            Robot[] home = null, opp = null;
            if (side == Side.Blue)
            {
                (home, opp) = (BlueRobots, YellowRobots);
            }
            else if (side == Side.Yellow)
            {
                (home, opp) = (YellowRobots, BlueRobots);
            }
            si.home = (Robot[])home.Clone();
            si.opp = (from rb in opp
                      select new OpponentRobot { pos = rb.pos, rotation = rb.rotation }).ToArray();
            if (side == Side.Yellow) si.ConvertToAnotherSide();
            si.TickMatch = TickMatch;
            return si;
        }
        
    }

    public struct MatchScore
    {
        public int BlueScore;
        public int YellowScore;

        public void Swap()
        {
            var tmp = BlueScore;
            BlueScore = YellowScore;
            YellowScore = tmp;
        }
    }

    public class SideInfo
    {
        public Robot[] home = new Robot[Const.RobotsPerTeam];
        public OpponentRobot[] opp = new OpponentRobot[Const.RobotsPerTeam];
        public Ball currentBall;
        public int TickMatch;

        public void ConvertToAnotherSide()
        {
            double ht = Const.Field.Right + Const.Field.Left;
            double vt = Const.Field.Bottom + Const.Field.Top;

            currentBall.pos.x = ht - currentBall.pos.x;
            currentBall.pos.y = vt - currentBall.pos.y;
            for (int i = 0; i < Const.RobotsPerTeam; i++)
            {
                home[i].pos.x = ht - home[i].pos.x;
                home[i].pos.y = vt - home[i].pos.y;
                if (home[i].rotation > 0)
                {
                    home[i].rotation = home[i].rotation - 180;
                }
                else if (home[i].rotation <= 0)
                {
                    home[i].rotation = home[i].rotation + 180;
                }

                opp[i].pos.x = ht - opp[i].pos.x;
                opp[i].pos.y = vt - opp[i].pos.y;
                if (opp[i].rotation > 0)
                {
                    opp[i].rotation = opp[i].rotation - 180;
                }
                else if (opp[i].rotation <= 0)
                {
                    opp[i].rotation = opp[i].rotation + 180;
                }
            }
        }
    }

    public class PlacementInfo
    {
        public Robot[] Robots = new Robot[Const.RobotsPerTeam];
        public Ball Ball = new Ball();

        public  void PlacementInfoFromMatchInfo(MatchInfo matchInfo,Side side)
        {
            Ball = matchInfo.Ball;
            if(side == Side.Blue)
            {
                Robots = (Robot[])matchInfo.BlueRobots.Clone();
            }
            else if(side == Side.Yellow)
            {
                Robots = (Robot[])matchInfo.YellowRobots.Clone();
            }
            else
            {
                
            }
        }
        public void Normalize()
        {
            // TODO 保证不会出界、不会重叠
            for (int i = 0; i < Const.RobotsPerTeam; i++)
            {
                Robots[i].Normalize(
                    Const.Field.Right - 10 * i + 1,
                    Const.Field.Left - 10 * i + 1,
                    Const.Field.Top - 10 * i + 1,
                    Const.Field.Bottom - 10 * i + 1
                    );
            }
            Ball.Normalize();
        }

        public void ConvertToAnotherSide()
        {
            double ht = Const.Field.Right + Const.Field.Left;
            double vt = Const.Field.Bottom + Const.Field.Top;

            Ball.pos.x = ht - Ball.pos.x;
            Ball.pos.y = vt - Ball.pos.y;
            for (int i = 0; i < Const.RobotsPerTeam; i++)
            {
                Robots[i].pos.x = ht - Robots[i].pos.x;
                Robots[i].pos.y = vt - Robots[i].pos.y;
                if (Robots[i].rotation > 0)
                {
                    Robots[i].rotation = Robots[i].rotation - 180;
                }
                else if (Robots[i].rotation <= 0)
                {
                    Robots[i].rotation = Robots[i].rotation + 180;
                }
            }
        }
    }

    [Serializable]
    public class WheelInfo
    {
        public Wheel[] Wheels = new Wheel[Const.RobotsPerTeam];

        public WheelInfo() { }

        public void Normalize()
        {
            for (int i = 0; i < Const.RobotsPerTeam; i++)
            {
                Wheels[i].Normalize();
            }
        }
    }
    
    
    
    public struct Vector2D
    {
        public double x;
        public double y;

        public static Vector2D Zero => new Vector2D();
        
        public Vector2D(double x , double y)
        {
            this.x = x;
            this.y = y;
        }
        
        public void ClampToRect(double right, double left, double top, double bottom)
        {
            // normalize to the specified box
            if (x < left)
            {
                x = left;
            }
            else if (x > right)
            {
                x = right;
            }
            
            if (y < bottom)
            {
                y = bottom;
            }
            else if (y > top)
            {
                y = top;
            }
        }
        
        public void ClampToField()
        {
            ClampToRect(Const.Field.Right, Const.Field.Left, Const.Field.Top, Const.Field.Bottom);
        }

        public static Vector2D operator +(Vector2D lhs, Vector2D rhs)
        {
            return new Vector2D(lhs.x + rhs.x, lhs.y + rhs.y);
        }

        public static Vector2D operator -(Vector2D vec)
        {
            return new Vector2D(-vec.x, -vec.y);
        }

        public static Vector2D operator -(Vector2D lhs, Vector2D rhs)
        {
            return lhs + (-rhs);
        }

        public static double operator *(Vector2D lhs, Vector2D rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y;
        }

        public bool IsNotNear(Vector2D rhs)
        {
            if (Math.Abs(this.x - rhs.x) > 0.1 || Math.Abs(this.y - rhs.y) > 0.1)
            {
                return true;
            }

            return false;
        }

        public static Vector2D operator /(Vector2D vec, double v)
        {
            return new Vector2D(vec.x / v, vec.y / v);
        }

        public double Cross(Vector2D rhs)
        {
            return x * rhs.y - y * rhs.x;
        }
        
        public bool Equals(Vector2D rhs)
        {
            if (x == rhs.x && y == rhs.y)
                return true;
            else
                return false;
        }

        public static double Distance (Vector2D lhs, Vector2D rhs)
        {
            return (double)Math.Sqrt(Math.Pow(lhs.x - rhs.x, 2) + Math.Pow(lhs.y - rhs.y, 2));
        }
        
        /// <summary>
        /// 旋转向量
        /// </summary>
        /// <param name="angle">逆时针旋转角，采用弧度制</param>
        /// <returns></returns>
        public Vector2D Rotate(double angle)
        {
            return new Vector2D(
                (double)(x * Math.Cos(angle) - y * Math.Sin(angle)),
                (double)(x * Math.Sin(angle) + y * Math.Cos(angle)));
        }
    }

    [Serializable]
    public struct Wheel
    {
        public double left;
        public double right;

        public void Normalize()
        {
            if (left < Const.MinWheelVelocity)
            {
                left = Const.MinWheelVelocity;
            }
            else if (left > Const.MaxWheelVelocity)
            {
                left = Const.MaxWheelVelocity;
            }
            
            if (right < Const.MinWheelVelocity)
            {
                right = Const.MinWheelVelocity;
            }
            else if (right > Const.MaxWheelVelocity)
            {
                right = Const.MaxWheelVelocity;
            }
        }
    }

    public struct Robot
    {
        public double mass;
        public Vector2D pos;
        public double rotation;
        public Wheel wheel;
        public Vector2D linearVelocity;
        public double angularVelocity;

        public void Normalize(double right, double left, double top, double bottom)
        {
            pos.ClampToRect(right, left, top, bottom);
            wheel.Normalize();
        }

        public void Normalize()
        {
            pos.ClampToField();
            wheel.Normalize();
        }

        public bool Equals(Robot robot)
        {
            if (pos.Equals(robot.pos) && rotation == robot.rotation)
                return true;
            else
                return false;
        }
    }

    public struct OpponentRobot
    {
        public Vector2D pos;
        public double rotation;
    }

    public struct Ball
    {
        public enum WhichDoor
        {
            BlueDoor,
            YellowDoor,
            None,
        }

        public double mass;
        public Vector2D pos;
        public Vector2D linearVelocity;
        public double angularVelocity;

        public void moveTo(double x, double y)
        {
            pos.x = x;
            pos.y = y;
        }

        public WhichDoor IsInDoor()
        {
            if (pos.x > Const.Field.Right)
            {
                return WhichDoor.BlueDoor;
            }
            else if (pos.x < Const.Field.Left)
            {
                return WhichDoor.YellowDoor;
            }
            else
            {
                return WhichDoor.None;
            }
        }

        public void Normalize(double right, double left, double top, double bottom)
        {
            pos.ClampToRect(right, left, top, bottom);
        }

        public void Normalize()
        {
            pos.ClampToField();
        }
    }

    public class TeamInfo
    {
        public string Name { get; set; }
    }

    public static class Extended
    {
        public static Side ToAnother(this Side side)
        {
            switch (side)
            {
                case Side.Blue:
                    return Side.Yellow;
                case Side.Yellow:
                    return Side.Blue;
                default:
                    return Side.Nobody;
            }
        }

        public static ResultType ToAnother(this ResultType resultType)
        {
            switch(resultType)
            {
                case ResultType.FreeKickLeftBot:
                    return ResultType.FreeKickRightTop;
                case ResultType.FreeKickRightTop:
                    return ResultType.FreeKickLeftBot;
                case ResultType.FreeKickLeftTop:
                    return ResultType.FreeKickRightBot;
                case ResultType.FreeKickRightBot:
                    return ResultType.FreeKickLeftTop;
                default:
                    return resultType;
            }
        }

        public static MatchPhase NextPhase(this MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.FirstHalf:
                    return MatchPhase.SecondHalf;
                case MatchPhase.SecondHalf:
                    return MatchPhase.OverTime;
                case MatchPhase.OverTime:
                    return MatchPhase.Penalty;
                default:
                    throw new ArgumentException("Penalty don't have next phase");
            }
        }
    }
}
