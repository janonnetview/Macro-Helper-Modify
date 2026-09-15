// Testes em série.
//
// Cada BancoDeTeste chama SqliteConnection.ClearAllPools() ao ser descartado, e esse método é
// GLOBAL: rodando em paralelo, o descarte de um teste mexeria no pool de outro. A suíte inteira
// leva menos de um segundo, então não há nada a ganhar em paralelizar e há uma classe de
// intermitência a perder.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
