using System.Diagnostics;

using Cave.DeviceControllers;
using Cave.DeviceControllers.Projectors.NEC;

namespace CaveTests
{
    internal class Program
    {
        static void ExceptionTest()
        {
            Test1();
            Test2();
            Test3();
            Test4();
            Test5();
            Test6();
            Test7();
            Test8();
        }

        static void Test1()
        {
            try
            {
                Console.WriteLine("-----Test 1-----");
                throw NECProjectorException.CreateNewFromValues(0, 4);
            }
            catch (Exception ex)
            {
                // will print "Unknown NEC projector error" because the dictionary entry at (0, 4) is null
                Console.WriteLine(ex.Message);
            }
        }

        static void Test2()
        {
            try
            {
                Console.WriteLine("-----Test 2-----");
                throw new NECProjectorException("Eek! An error has happened!");
            }
            catch (Exception ex)
            {
                // will print "Eek! An error has happened!"
                Console.WriteLine(ex.Message);
            }
        }

        static void Test3()
        {
            try
            {
                Console.WriteLine("-----Test 3-----");
                throw NECProjectorException.CreateNewFromValues(0, 64);
            }
            catch ( Exception ex )
            {
                // will print "Lamp 1 failed to light"
                Console.WriteLine(ex.Message);
            }
        }

        static void Test4()
        {
            try
            {
                Console.WriteLine("-----Test 4-----");
                // will throw an exception itself, bad byteKey
                throw NECProjectorException.CreateNewFromValues(4, 99);
            }
            catch ( Exception ex )
            {
                // will print ArgumentOutOfRangeException's message
                Console.WriteLine(ex.Message);
            }
        }

        static void Test5()
        {
            try
            {
                Console.WriteLine("-----Test 5-----");
                // will throw an exception itself, bad bitKey
                throw NECProjectorException.CreateNewFromValues(0, 99);
            }
            catch ( Exception ex )
            {
                // will print ArgumentOutOfRangeException's message
                Console.WriteLine(ex.Message);
            }
        }


        static void Test6()
        {
            try
            {
                Console.WriteLine("-----Test 6-----");
                throw NECProjectorCommandException.CreateNewFromValues(0x02, 0x0f, Command.GetStatus);
            }
            catch ( Exception ex )
            {
                // will print "There is no authority necessary for the operation."
                Console.WriteLine(ex.Message);
                // will print "NECProjectorCommandException: There is no authority necessary for the operation."
                //            "     ErrorCode :     020f"
                //            "     Command   :     GetStatus"
                Console.WriteLine(ex);
            }
        }

        static void Test7()
        {
            try
            {
                Console.WriteLine("-----Test 7-----");
                // will throw ArgumentOutOfRangeException
                throw NECProjectorCommandException.CreateNewFromValues(0xf0, 0x0d, Command.SelectInput);
            }
            catch ( Exception ex )
            {
                // message only
                Console.WriteLine(ex.Message);
                // stacktrace and all
                Console.WriteLine(ex);
            }
        }

        static void Test8()
        {
            try
            {
                Console.WriteLine("-----Test 8-----");
                throw new NECProjectorCommandException("No can do.");
            }
            catch ( Exception ex )
            {
                // will print "NECProjectorCommandException: No can do."
                Console.WriteLine(ex);
            }
        }

        static void Main( string[] args )
        {
            ExceptionTest();
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }
    }
}
