using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Text.Json;
using System.IO;


public class PortfolioOptimizer
{
    public PortfolioOptimizer()
    {
        rnd = new();
        Init();
    }

    public PortfolioOptimizer(int N, int G, int n, double[] Prices, double[] Returns, double[][] Covariances, double B, double Fee, double r, double S, int[] OldPortfolio)
    {
        rnd = new();
        this.N = N;
        this.G = G;
        this.n = N;
        this.Prices = Prices;
        this.Returns = Returns;
        this.Covariances = Covariances;
        this.B = B;
        this.Fee = Fee;
        this.r = r;
        this.S = S;
        this.OldPortfolio = OldPortfolio;

    }
    public int N {get;set;}
    public int G {get;set;}
    public int n {get;set;}
    public double[] Prices {get;set;} //цены на активы
    public double [] Returns {get;set;} //вектор доходностей
    public double[][] Covariances {get;set;}//ковариационная матрица
    public double B {get;set;}
    public double Fee {get;set;}
    double r = 0.25; //стоимость шортовых позиций, в годовых
    double S = 0.8; //максимально допустимое отношение коротких позиций к капиталу
    public int[] OldPortfolio {get;set;}
    Random rnd;
    int seed = 172782205;

    public void Init()
    {
        N = 500;
        G = 1000;
        n = 10;
        OldPortfolio = new int[n];
        for(int i = 0; i < n; i++) OldPortfolio[i] = 0;
        B = 1000_000;
        Fee = 0.001;
        Returns = new double[n];
        Prices = new double[n];
        Covariances = new double[n][];
        for(int i = 0; i < n; i++) Covariances[i] = new double[n];

        for(int i = 0; i < n; i++)
        {
            double sign = rnd.NextSingle() < 0.8 ? 1.0 : -1.0;
            Returns[i] = rnd.NextDouble() * sign;
            Prices[i] = rnd.Next(1000);
        } 
        
        var R = DenseMatrix.Create(n, n, (i, j) => rnd.NextDouble()* 2.0 - 1.0);
        var Vmatrix = R * R.Transpose(); 
        
        for(int i = 0; i < n; i++)
            for(int j = 0; j < n; j++)
                Covariances[i][j] = Vmatrix[i, j];
    }
    public class Individual
    {
        PortfolioOptimizer portfolioOptimizer;
        //int n =10;
        public int[] Genome = Array.Empty<int>();
        public double Fitness = double.NegativeInfinity;

        public Individual(PortfolioOptimizer po, int[] genome)
        {
            portfolioOptimizer = po;
            Genome = genome;
            Fitness = po.CalculateFitness(this);
        }
        public Individual(PortfolioOptimizer po)
        {
            portfolioOptimizer = po;
            Genome = new int[po.n];
        }

        public double CalculateReturn()
        {
            double sum = 0;
            for(int i = 0; i < portfolioOptimizer.n; i++)
            {
                sum += portfolioOptimizer.Returns[i] * Genome[i] * portfolioOptimizer.Prices[i];
            }
            return sum / portfolioOptimizer.B;
        }

        public double CalculateRisk()
        {
            double sum = 0;
            for(int i = 0; i < portfolioOptimizer.n; i++)
            {
                for(int j = 0; j < portfolioOptimizer.n; j++)
                {
                    sum += Genome[i] * Genome[j] * portfolioOptimizer.Covariances[i][j] * portfolioOptimizer.Prices[i] * portfolioOptimizer.Prices[j];
                }
            }
            return sum / Math.Pow(portfolioOptimizer.B, 2);
        }

        public double CalculateCost()
        {
            double sum = 0; 
            for(int i = 0 ;i < portfolioOptimizer.n; i++)
            {
                sum += Genome[i] * portfolioOptimizer.Prices[i] + portfolioOptimizer.Fee * Math.Abs(portfolioOptimizer.OldPortfolio[i] - Genome[i]);
                if(Genome[i] < 0) sum -= portfolioOptimizer.r * Genome[i] * portfolioOptimizer.Prices[i];
            }
            return sum;
        }

        public void Rectify()
        {
            /*double _sum = Genome.Sum();
            for(int i = 0; i < portfolioOptimizer.n; i++) Genome[i] /= _sum;

            _sum = Genome.Sum();
            for(int i = 0; i < portfolioOptimizer.n; i++) Genome[i] /= _sum;*/

            Fitness = portfolioOptimizer.CalculateFitness(this);
        }

        public override string ToString()
        {
            string str = "x = ";
            for(int i = 0 ; i < portfolioOptimizer.n; i++)
                str += Genome[i].ToString() + "  ";
            return str;
        }
    }

    public double CalculateFitness(Individual ind)
    {
        double sum_shorts = 0;
        for(int i = 0; i < n; i++)
        {
            if(ind.Genome[i] < 0) sum_shorts += ind.Genome[i] * Prices[i];
        }
        double shorts_violation = Math.Max(0, sum_shorts - S*B);
        return Utility(ind) - 10 * Math.Pow((ind.CalculateCost() - B), 2) - 10 * shorts_violation;
    }

    public static double Utility(Individual ind)
    {
        return ind.CalculateReturn() - ind.CalculateRisk();
    }
    

    

    public Individual Solve() //решение генетическим алгоритмом
    {
        Individual[] population = new Individual[N];

        for(int i = 0; i < N; i++) //инициализация
        {
            population[i] = new Individual(this);
            for(int j = 0; j < n; j++)
            {
                int sign = rnd.NextDouble() > 0.5 ? 1 : -1;
                population[i].Genome[j] = rnd.Next((int)(B / Prices[j])) * sign;
            }
            population[i].Rectify();
        }  

        Individual TournamentSelection()
        {
            double maxFitness = double.MinValue;
            int index = 0;
            for(int i = 0; i < 4; i++)
            {
                int current_index = rnd.Next(N);
                if(population[current_index].Fitness > maxFitness)
                {
                    maxFitness = population[current_index].Fitness;
                    index = current_index;
                }
            }
            return population[index];
        }

        void Mutate(Individual x)
        {
            int index = rnd.Next(n);
            int sign = rnd.Next() > 0.5 ? 1 : -1;
            int h = rnd.Next((int)(B / Prices[index])) * sign;
            x.Genome[index] += h;
        }



        for(int g = 0; g < G; g++)
        {
            Individual[] new_population = new Individual[N];

            for(int i = 0; i < N; i+=2)
            {
                Individual parent1 = TournamentSelection();
                Individual parent2 = TournamentSelection();
        
                //скрещивание
                Individual child1 = new Individual(this);
                Individual child2 = new Individual(this);

                double alpha = 0.4;
                for(int j = 0; j < n; j++)
                {
                    child1.Genome[j] = (int)(alpha * parent1.Genome[j] + (1.0 - alpha) * parent2.Genome[j]);
                    child2.Genome[j] = (int)((1.0 - alpha) * parent1.Genome[j] + alpha * parent2.Genome[j]);
                }
                
                //мутация
                Mutate(child1);
                Mutate(child2);

                child1.Rectify();
                child2.Rectify();

                new_population[i] = child1;
                new_population[i+1] = child2;
                
                
            }
            Array.Sort(new_population, (a, b) => b.Fitness.CompareTo(a.Fitness));
            Array.Sort(population, (a, b) => b.Fitness.CompareTo(a.Fitness));
            Array.Copy(new_population, 0, population, N/10, N*9/10);
        }
        return population[0];
    }


    static void Main()
    {
        

        //PortfolioOptimizer po = new();
        PortfolioOptimizer po = PortfolioOptimizer.LoadFromJSON("data.json");
        Console.WriteLine("return = ");
        for(int i = 0; i < po.n; i++)
        {
            Console.Write(Math.Round(po.Returns[i], 4).ToString() + " ");
        }

        Console.WriteLine("\n\nCovariance matrix: ");
        for(int i = 0; i < po.n; i++)
        {
            for(int j = 0; j < po.n; j++)
            {
                Console.Write(Math.Round(po.Covariances[i][j], 4).ToString() + " ");
            }
            Console.WriteLine();
        }
        Console.WriteLine("\n ===================================== \n");
    
        Individual best = po.Solve();
        
        Console.WriteLine(best.ToString());
        Console.WriteLine("return = " + best.CalculateReturn().ToString() + ", risk = " + best.CalculateRisk().ToString() + ", total cost = " + best.CalculateCost().ToString());
        
    }

    public void SaveToJson(string fileName)
    {
        string json = JsonSerializer.Serialize<PortfolioOptimizer>(this);
        using(StreamWriter sw = new StreamWriter(fileName))
        {
            sw.Write(json);
        }
    }

    public static PortfolioOptimizer LoadFromJSON(string fileName)
    {
        string json;
        using(StreamReader sr = new StreamReader(fileName))
        {
            json = sr.ReadLine();
        }
        PortfolioOptimizer po = JsonSerializer.Deserialize<PortfolioOptimizer>(json);
        return po;
    }
}