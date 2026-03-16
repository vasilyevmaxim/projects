namespace ProcedureMapGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ProcedureGenerator pg = new ProcedureGenerator(20, 20);
            pg.GenerateMazeLikeMap(40);
            pg.PrintMapAsGraphCompact();

            pg.GenerateTunnelMap(40);
            pg.PrintMapAsGraphCompact();

            pg.GenerateMap(40, 0.1, 1, 2);
            pg.PrintMapAsGraphCompact();
        }
    }
}
                                                                                                                