using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Diagnostics;
using System.Linq; // Needed for array cloning
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace GaussSolver
{
    class Program
    {
        // === MAIN MENU ===
        static void Main(string[] args)
        {
            // BenchmarkDotNet requires Release mode.
            // If the user selects Option 6, we run the professional benchmark.

            // === ADD THESE 3 LINES AT THE TOP ===
            Console.BackgroundColor = ConsoleColor.White; // Set background to White
            Console.ForegroundColor = ConsoleColor.Black; // Set text to Black
            Console.Clear();                              // Apply changes to the whole screen immediately

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== COURSE PROJECT: GAUSS SOLVER ===");
                Console.WriteLine("1. Run Module Test (Verify Logic with small 2x2)");
                Console.WriteLine("2. Generate Test Data (Create system.xml)");
                Console.WriteLine("3. Solve System (Read system.xml and Solve)");
                Console.WriteLine("4. BENCHMARK (Quick Compare: Sequential vs Parallel)");
                Console.WriteLine("5. PROFESSIONAL BENCHMARK (BenchmarkDotNet - Wait 2 mins)");
                Console.WriteLine("6. Exit");
                Console.Write("\nSelect an option: ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": Tester.RunModuleTest(); break;
                    case "2": Generator.Run(); break;
                    case "3": Solver.Run(false); break; // Run Normal
                    case "4": Solver.Run(true); break;  // Run Quick Benchmark
                    case "5":
                        // This launches the professional tool
                        Console.WriteLine("Running BenchmarkDotNet... This takes time!");
                        var summary = BenchmarkRunner.Run<GaussBenchmark>();
                        break;
                    case "6": return;
                    default: Console.WriteLine("Invalid choice."); break;
                }
                Console.WriteLine("\nPress Enter to return to menu...");
                Console.ReadLine();
            }
        }
    }

    // === NEW CLASS: PROFESSIONAL BENCHMARK ===
    [MemoryDiagnoser] // This tells us how much RAM was used (Extra Marks!)
    public class GaussBenchmark
    {
        private double[][] A;
        private double[] b;
        private int N = 500; // Benchmark size (Keep it moderate so it doesn't take forever)

        // Setup runs ONCE before the actual tests
        [GlobalSetup]
        public void Setup()
        {
            // We use the Generator logic to create data in memory directly
            Random rand = new Random(42); // Fixed seed for consistency
            A = new double[N][];
            b = new double[N];

            for (int i = 0; i < N; i++)
            {
                A[i] = new double[N];
                for (int j = 0; j < N; j++)
                {
                    A[i][j] = rand.NextDouble() * 100.0;
                }
                b[i] = rand.NextDouble() * 100.0;
            }
        }

        // Helper to clone data so one test doesn't ruin the matrix for the next test
        private double[][] CloneMatrix(double[][] source)
        {
            return source.Select(s => (double[])s.Clone()).ToArray();
        }

        [Benchmark(Baseline = true)] // This is our standard to compare against
        public void Sequential()
        {
            var A_copy = CloneMatrix(A);
            var b_copy = (double[])b.Clone();
            Solver.SequentialGaussianElimination(A_copy, b_copy);
        }

        [Benchmark] // This is the candidate to test
        public void Parallel()
        {
            var A_copy = CloneMatrix(A);
            var b_copy = (double[])b.Clone();
            Solver.ParallelGaussianElimination(A_copy, b_copy);
        }
    }

    // === PART 1: THE GENERATOR ===
    class Generator
    {
        public static void Run()
        {
            Console.Write("\nEnter number of unknowns (N): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n <= 0) { Console.WriteLine("Invalid."); return; }

            string filename = "system.xml";
            Console.WriteLine($"Generating {n}x{n} system... Please wait.");
            Random rand = new Random();

            using (StreamWriter sw = new StreamWriter(filename, false, Encoding.UTF8, 65536))
            {
                sw.WriteLine($"<system n=\"{n}\">");
                sw.WriteLine("  <matrix>");
                for (int i = 0; i < n; i++)
                {
                    sw.Write("    <row>");
                    for (int j = 0; j < n; j++)
                    {
                        sw.Write((rand.NextDouble() * 100.0).ToString("F2", CultureInfo.InvariantCulture));
                        if (j < n - 1) sw.Write(" ");
                    }
                    sw.WriteLine("</row>");
                }
                sw.WriteLine("  </matrix>");
                sw.WriteLine("  <vector>");
                for (int i = 0; i < n; i++)
                {
                    sw.Write((rand.NextDouble() * 100.0).ToString("F2", CultureInfo.InvariantCulture));
                    if (i < n - 1) sw.Write(" ");
                }
                sw.WriteLine("  </vector>");
                sw.WriteLine("</system>");
            }
            Console.WriteLine("File generated successfully!");
        }
    }

    // === PART 2: THE SOLVER ===
    class Solver
    {
        public static void Run(bool benchmarkMode)
        {
            string fileName = "system.xml";
            if (!File.Exists(fileName)) { Console.WriteLine("Error: Generate data first (Option 2)."); return; }

            try
            {
                Console.WriteLine("Reading XML...");
                double[][] A_seq, A_par;
                double[] b_seq, b_par;
                int n;

                ReadSystemFromXml(fileName, out A_par, out b_par, out n);

                if (benchmarkMode)
                {
                    // === QUICK BENCHMARK MODE (Naive Stopwatch) ===
                    Console.WriteLine("Cloning matrix for comparison...");
                    A_seq = new double[n][];
                    b_seq = new double[n];
                    Array.Copy(b_par, b_seq, n);
                    for (int i = 0; i < n; i++)
                    {
                        A_seq[i] = new double[n];
                        Array.Copy(A_par[i], A_seq[i], n);
                    }

                    Console.Write("Running Sequential (Normal) Gauss... ");
                    var watch1 = Stopwatch.StartNew();
                    SequentialGaussianElimination(A_seq, b_seq);
                    watch1.Stop();
                    Console.WriteLine($"Done: {watch1.ElapsedMilliseconds} ms");

                    Console.Write("Running Parallel (Fast) Gauss...      ");
                    var watch2 = Stopwatch.StartNew();
                    ParallelGaussianElimination(A_par, b_par);
                    watch2.Stop();
                    Console.WriteLine($"Done: {watch2.ElapsedMilliseconds} ms");

                    double speedup = (double)watch1.ElapsedMilliseconds / Math.Max(1, watch2.ElapsedMilliseconds);
                    Console.WriteLine($"\nSpeedup: {speedup:F2}x");
                }
                else
                {
                    // === NORMAL SOLVE MODE ===
                    Console.WriteLine($"Solving {n}x{n} system (Parallel)...");
                    var watch = Stopwatch.StartNew();
                    double[] x = ParallelGaussianElimination(A_par, b_par);
                    watch.Stop();
                    Console.WriteLine($"Solved in {watch.ElapsedMilliseconds} ms.");

                    // 1. ALWAYS SAVE to file
                    string outputFile = "solution.txt";
                    File.WriteAllLines(outputFile, Array.ConvertAll(x, v => v.ToString("F4", CultureInfo.InvariantCulture)));
                    Console.WriteLine($"[OK] Solution saved to '{outputFile}'");

                    // 2. SHOW ON SCREEN if small (<= 20)
                    if (n <= 20)
                    {
                        Console.WriteLine("\n--- CALCULATED SOLUTION ---");
                        for (int i = 0; i < n; i++)
                        {
                            Console.WriteLine($"x[{i}] = {x[i]:F4}");
                        }
                        Console.WriteLine("---------------------------");
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Error: " + ex.Message); }
        }

        static void ReadSystemFromXml(string fileName, out double[][] A, out double[] b, out int n)
        {
            A = null; b = null; n = 0;
            int currentRow = 0;
            var culture = CultureInfo.InvariantCulture;
            using (XmlReader reader = XmlReader.Create(fileName))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "system")
                        {
                            n = int.Parse(reader.GetAttribute("n"));
                            A = new double[n][]; b = new double[n];
                        }
                        else if (reader.Name == "row")
                        {
                            string content = reader.ReadElementContentAsString();
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                string[] parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                A[currentRow] = new double[n];
                                for (int j = 0; j < n; j++) A[currentRow][j] = double.Parse(parts[j], culture);
                                currentRow++;
                            }
                        }
                        else if (reader.Name == "vector")
                        {
                            string content = reader.ReadElementContentAsString();
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                string[] parts = content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                for (int i = 0; i < n; i++) b[i] = double.Parse(parts[i], culture);
                            }
                        }
                    }
                }
            }
        }

        // Made PUBLIC so Benchmark can use it
        public static double[] SequentialGaussianElimination(double[][] A, double[] b)
        {
            int n = b.Length;
            for (int k = 0; k < n - 1; k++)
            {
                int maxRow = k;
                double maxVal = Math.Abs(A[k][k]);
                for (int i = k + 1; i < n; i++) if (Math.Abs(A[i][k]) > maxVal) { maxVal = Math.Abs(A[i][k]); maxRow = i; }
                if (maxRow != k) { double[] t = A[k]; A[k] = A[maxRow]; A[maxRow] = t; double tb = b[k]; b[k] = b[maxRow]; b[maxRow] = tb; }

                for (int i = k + 1; i < n; i++)
                {
                    double factor = A[i][k] / A[k][k];
                    for (int j = k + 1; j < n; j++) A[i][j] -= factor * A[k][j];
                    b[i] -= factor * b[k];
                }
            }
            return BackSubstitution(A, b, n);
        }

        // Made PUBLIC so Benchmark can use it
        public static double[] ParallelGaussianElimination(double[][] A, double[] b)
        {
            int n = b.Length;
            for (int k = 0; k < n - 1; k++)
            {
                int maxRow = k;
                double maxVal = Math.Abs(A[k][k]);
                for (int i = k + 1; i < n; i++) if (Math.Abs(A[i][k]) > maxVal) { maxVal = Math.Abs(A[i][k]); maxRow = i; }
                if (maxRow != k) { double[] t = A[k]; A[k] = A[maxRow]; A[maxRow] = t; double tb = b[k]; b[k] = b[maxRow]; b[maxRow] = tb; }

                Parallel.For(k + 1, n, i =>
                {
                    double factor = A[i][k] / A[k][k];
                    for (int j = k + 1; j < n; j++) A[i][j] -= factor * A[k][j];
                    b[i] -= factor * b[k];
                });
            }
            return BackSubstitution(A, b, n);
        }

        static double[] BackSubstitution(double[][] A, double[] b, int n)
        {
            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = 0.0;
                for (int j = i + 1; j < n; j++) sum += A[i][j] * x[j];
                x[i] = (b[i] - sum) / A[i][i];
            }
            return x;
        }
    }

    // === PART 3: THE TESTER ===
    class Tester
    {
        public static void RunModuleTest()
        {
            Console.WriteLine("\n=== RUNNING MODULE TEST (2x2) ===");

            // 1. Define a simple problem
            // 2x + 1y = 5
            // -3x + 4y = 9
            // Expected Solution: x = 1, y = 3

            double[][] A = new double[2][];
            A[0] = new double[] { 2.0, 1.0 };
            A[1] = new double[] { -3.0, 4.0 };
            double[] b = new double[] { 5.0, 9.0 };

            // 2. Show the Problem
            Console.WriteLine("System to solve:");
            Console.WriteLine($" {A[0][0]}x + {A[0][1]}y = {b[0]}");
            Console.WriteLine($" {A[1][0]}x + {A[1][1]}y = {b[1]}");

            // 3. Solve using the REAL Parallel Solver
            Console.WriteLine("\nCalculating...");
            double[] result = Solver.ParallelGaussianElimination(A, b);

            // 4. Show the Result
            Console.WriteLine($"\nResult found: x={result[0]:F2}, y={result[1]:F2}");
            Console.WriteLine("Expected:      x=1.00, y=3.00");

            // 5. Verify
            bool passX = Math.Abs(result[0] - 1.0) < 0.0001;
            bool passY = Math.Abs(result[1] - 3.0) < 0.0001;

            if (passX && passY)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[PASS] Logic Verified Successfully.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[FAIL] Calculations are incorrect.");
            }
            Console.ResetColor();
        }
    }
}



