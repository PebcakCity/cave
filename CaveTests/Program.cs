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
            Test9();
            Test10();
            Test11();
            Test12();
        }

        static void Test1()
        {
            try
            {
                Console.WriteLine("-----Test 1-----");
                throw new NECProjectorException(0, 4);
            }
            catch (Exception ex)
            {
                // will print "Unknown NEC projector error" because the default message in the dictionary is null
                Console.WriteLine(ex.Message);
            }
        }

        static void Test2()
        {
            try
            {
                Console.WriteLine("-----Test 2-----");
                throw new NECProjectorException(0, 4, "Eek! An error has happened!");
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
                throw new NECProjectorException(0, 64);
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
                throw new NECProjectorException(0, 64, "Eek! Another error!!");
            }
            catch ( Exception ex )
            {
                // will print "Eek! Another error!!"
                Console.WriteLine(ex.Message);
            }
        }

        static void Test5()
        {
            try
            {
                Console.WriteLine("-----Test 5-----");
                // constructor will throw an exception itself, bad byteKey
                throw new NECProjectorException(4, 99, "This message will never print!");
            }
            catch ( Exception ex )
            {
                // will instead print "byteKey=4: bad argument to NECProjectorException constructor."
                Console.WriteLine(ex.Message);
            }
        }

        static void Test6()
        {
            try
            {
                Console.WriteLine("-----Test 6-----");
                // constructor will throw an exception itself, bad bitKey
                throw new NECProjectorException(0, 99, "This message will never print either!");
            }
            catch ( Exception ex )
            {
                // will instead print "bitKey=99: bad argument to NECProjectorException constructor."
                Console.WriteLine(ex.Message);
            }
        }

        static void Test7()
        {
            try
            {
                Console.WriteLine("-----Test 7-----");
                throw new NECProjectorException();
            }
            // catch the parent class
            catch ( DeviceException ex )
            {
                // will print "Unknown NEC projector error."
                Console.WriteLine(ex.Message);
            }
        }

        static void Test8()
        {
            try
            {
                Console.WriteLine("-----Test 8-----");
                throw new NECProjectorCommandException(0x02, 0x0f);
            }
            catch ( Exception ex )
            {
                // will print "There is no authority necessary for the operation."
                Console.WriteLine(ex.Message);
                // will print "NECProjectorCommandException 020f - There is no authority necessary for the operation."
                Console.WriteLine(ex);
            }
        }

        static void Test9()
        {
            try
            {
                Console.WriteLine("-----Test 9-----");
                throw new NECProjectorCommandException(0x02, 0x0f, "A custom message.");
            }
            catch ( Exception ex )
            {
                // will print "A custom message."
                Console.WriteLine(ex.Message);
                // will print "NECProjectorCommandException 020f - A custom message."
                Console.WriteLine(ex);
            }
        }

        static void Test10()
        {
            try
            {
                Console.WriteLine("-----Test 10-----");
                // constructor will throw an exception itself, bad tuple f00d
                throw new NECProjectorCommandException(0xf0, 0x0d, "Sprinkles.");
            }
            catch ( Exception ex )
            {
                // will print Message of ArgumentException - "(xx, xx): bad argument to NECProjectorCommandException constructor."
                Console.WriteLine(ex.Message);
                // will print Message and full stack trace for the ArgumentException
                Console.WriteLine(ex);
            }
        }

        static void Test11()
        {
            try
            {
                Console.WriteLine("-----Test 11-----");
                throw new NECProjectorCommandException();
            }
            // catch the parent
            catch( DeviceCommandException ex )
            {
                Console.WriteLine(ex.Message);
                // I temporarily made ErrorTuple public... as expected, it's (0, 0) (int default)
                // Console.WriteLine(((NECProjectorCommandException) ex).ErrorTuple);
            }
        }

        static void Test12()
        {
            try
            {
                // DeviceException -> DeviceCommandException -> NECProjectorCommandException
                Console.WriteLine("-----Test 12-----");
                throw new NECProjectorCommandException();
            }
            // catch the grandparent
            catch ( DeviceException ex )
            {
                Console.WriteLine($"I caught this for you! It almost slipped by... {ex}");
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
