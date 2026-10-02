using System;
using Si_Formation;
static class Program
{
    static int checks;
    static void Check(bool condition,string message)
    {checks++;if(!condition)throw new Exception(message);}
    static void Main()
    {
        Check(CoordinationPolicy.NeedsSpeedCap(25,8),"faster member capped");
        Check(!CoordinationPolicy.NeedsSpeedCap(8,8),"slowest exempt");
        Check(!CoordinationPolicy.NeedsSpeedCap(8,8),"every tied slowest exempt");
        Check(!CoordinationPolicy.NeedsSpeedCap(25,25),"single unit exempt");
        Check(!CoordinationPolicy.NeedsSpeedCap(8,0),"invalid baseline never freezes units");
        Check(!CoordinationPolicy.NeedsSpeedCap(8,12),"removed slowest yields recalculated exemption");
        Check(CoordinationPolicy.NeedsSpeedCap(25,12),"remaining fast unit gets new baseline");
        Check(!CoordinationPolicy.NeedsSpeedCap(8.0001f,8),"float noise does not cap tied units");
        Console.WriteLine($"PASS: {checks} speed-cap policy checks. No Unity/runtime validation.");
    }
}