using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace iTunesTest
{
    static class Program
    {

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            var runs = new List<(string, TimeSpan)>();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 1; i++)
            {
                sw.Restart();
                DoStuff(collectGarbage: true, release: true);
                runs.Add(("collectGarbage: true, release: true", sw.Elapsed));
            }

            for (int i = 0; i < 1; i++)
            {
                sw.Restart();
                DoStuff(collectGarbage: true, release: false);
                runs.Add(("collectGarbage: true, release: false", sw.Elapsed));
            }

            for (int i = 0; i < 1; i++)
            {
                sw.Restart();
                DoStuff(collectGarbage: false, release: true);
                runs.Add(("collectGarbage: false, release: true", sw.Elapsed));
            }

            for (int i = 0; i < 1; i++)
            {
                sw.Restart();
                DoStuff(collectGarbage: false, release: false);
                runs.Add(("collectGarbage: false, release: false", sw.Elapsed));
            }

            foreach (var item in runs.GroupBy(x => x.Item1))
            {
                Console.WriteLine($"Average time for {item.Key} is {TimeSpan.FromSeconds(item.Average(x => x.Item2.TotalSeconds))}");
            }
            Console.ReadKey();
        }

        static IEnumerable<T> OfTypeComRelease<T>(this IEnumerable comObjects, bool release) where T : class
        {
            foreach (var o in comObjects)
            {
                var oT = o as T;
                if (oT != null)
                {
                    yield return (T)o;
                    continue;
                }
                if (release)
                {
                    Marshal.ReleaseComObject(o);
                }
            }
        }

        static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> collection, int chunkSize)
        {
            while (collection.Any())
            {
                yield return collection.Take(chunkSize);
                collection = collection.Skip(chunkSize);
            }
        }

        private static void DoStuff(bool collectGarbage, bool release)
        {
            iTunesLib.iTunesApp app = new iTunesLib.iTunesAppClass();
            var tracks = app.LibraryPlaylist.Tracks;
            Console.WriteLine($"🎵 {tracks.Count} Tracks, Querying...");
            //CultureInfo culture = CultureInfo.InvariantCulture;
            //var styles = new string[]
            //{
            //    @"%h\:%m\:%s",
            //    @"%m\:%s"
            //};

            // vector<int> alingment
            int arraySize = (int)Math.Ceiling((double)tracks.Count / Vector<int>.Count) * Vector<int>.Count;

            var durationArray = new int[arraySize];
            var countArray = new int[arraySize];

            foreach (var (track, i) in tracks
                .OfTypeComRelease<iTunesLib.IITTrack>(release)
                .Select((x, i) => (x, i)))
            {
                countArray[i] = track.PlayedCount;
                durationArray[i] = track.Duration;
                if (release)
                {
                    Marshal.ReleaseComObject(track);
                }

                if (i % 100 != 0)
                {
                    continue;
                }

                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write($"Track {i + 1}/{tracks.Count}".PadLeft(Console.WindowWidth));
                if (collectGarbage)
                {
                    GC.Collect();
                }
            }

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"Track {tracks.Count}/{tracks.Count}".PadLeft(Console.WindowWidth));
            Console.WriteLine();

            Console.WriteLine("Calculating....");

            long secCalc = 0;
            long secVector = 0;
            long secVector2 = 0;
            long secVector3 = 0;
            long ticksCalc = 0;
            long ticksVector = 0;
            long ticksVector2 = 0;
            long ticksVector3 = 0;

            var swCalc = Stopwatch.StartNew();
            secCalc = Calc(countArray, durationArray);
            swCalc.Stop();
            ticksCalc = swCalc.ElapsedTicks;

            swCalc.Restart();
            secVector = VectorCalc(countArray, durationArray);
            swCalc.Stop();
            ticksVector = swCalc.ElapsedTicks;
            swCalc.Restart();

            secVector2 = VectorCalc2(countArray, durationArray);
            swCalc.Stop();
            ticksVector2 = swCalc.ElapsedTicks;

            swCalc.Restart();
            secVector3 = VectorCalc3(countArray, durationArray);
            swCalc.Stop();
            ticksVector3 = swCalc.ElapsedTicks;


            var time = TimeSpan.FromSeconds(secVector);

            Console.WriteLine();
            Console.WriteLine($"total time: {time.TotalHours}h");
            Console.WriteLine($"\t {time.TotalDays}d");
            Console.WriteLine($"normal calc ticks: {ticksCalc}");
            Console.WriteLine($"vector calc ticks: {ticksVector}");
            Console.WriteLine($"vector2 calc ticks: {ticksVector2}");
            Console.WriteLine($"vector3 calc ticks: {ticksVector3}");

            Debug.Assert(secCalc == secVector, "vector calc wrong");
            Debug.Assert(secCalc == secVector2, "vector2 calc wrong");
            Debug.Assert(secCalc == secVector3, "vector3 calc wrong");

            Marshal.ReleaseComObject(app);
        }

        static long VectorCalc(int[] countArray, int[] durationArray)
        {
            if (countArray.Length % Vector<int>.Count != 0 ||
                countArray.Length != durationArray.Length)
            {
                // invalid input data
                return 0;
            }

            long result = 0;
            for (int i = 0; i < countArray.Length; i += Vector<int>.Count)
            {
                result += Vector.Dot(new Vector<int>(countArray, i) * new Vector<int>(durationArray, i), Vector<int>.One);
            }
            return result;
        }

        static long VectorCalc2(int[] countArray, int[] durationArray)
        {
            if (countArray.Length % Vector<int>.Count != 0 ||
                countArray.Length != durationArray.Length)
            {
                // invalid input data
                return 0;
            }

            long result = 0;
            var accVector = Vector<int>.Zero;
            for (int i = 0; i < countArray.Length; i += Vector<int>.Count)
            {
                accVector += new Vector<int>(countArray, i) * new Vector<int>(durationArray, i);
            }
            result = Vector.Dot(accVector, Vector<int>.One);
            return result;
        }

        static long VectorCalc3(int[] countArray, int[] durationArray)
        {
            if (countArray.Length % Vector<int>.Count != 0 ||
                countArray.Length != durationArray.Length)
            {
                // invalid input data
                return 0;
            }

            long result = 0;
            var accVectorArray = new Vector<int>[countArray.Length / Vector<int>.Count];
            for (int i = 0, l = 0; i < countArray.Length; i += Vector<int>.Count, l++)
            {
                accVectorArray[l] = new Vector<int>(countArray, i) * new Vector<int>(durationArray, i);
            }
            foreach (var vector in accVectorArray)
            {
                result += Vector.Dot(vector, Vector<int>.One);
            }
            return result;
        }

        static long Calc(int[] countArray, int[] durationArray)
        {
            long result = 0;
            foreach (var (count, duration) in countArray.Zip(durationArray))
            {
                result += count * duration;
            }
            return result;
        }
    }
}
