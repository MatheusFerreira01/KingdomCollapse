using KingdomCollapse.Core;
using NUnit.Framework;

namespace KingdomCollapse.Tests
{
    [TestFixture]
    public class ResourcePoolTests
    {
        [Test]
        public void PoolNasceVazio()
        {
            ResourcePool pool = new ResourcePool();

            for (int i = 0; i < ResourceKinds.All.Length; i++)
            {
                Assert.That(pool[ResourceKinds.All[i]], Is.Zero);
            }
        }

        [Test]
        public void CreditoEDebitoSaoIsoladosPorRecurso()
        {
            ResourcePool pool = new ResourcePool();

            pool.Add(ResourceKind.Wood, 10);

            Assert.That(pool[ResourceKind.Wood], Is.EqualTo(10));
            Assert.That(pool[ResourceKind.Stone], Is.Zero);
            Assert.That(pool[ResourceKind.Gold], Is.Zero);
        }

        [Test]
        public void DebitoNuncaDeixaSaldoNegativo()
        {
            ResourcePool pool = new ResourcePool(ResourceAmounts.Of(ResourceKind.Food, 3));

            int removed = pool.Remove(ResourceKind.Food, 10);

            Assert.That(removed, Is.EqualTo(3));
            Assert.That(pool[ResourceKind.Food], Is.Zero);
        }

        [Test]
        public void CustoAlemDoSaldo_ERecusadoSemAlterarNada()
        {
            ResourcePool pool = new ResourcePool(ResourceAmounts.Of(
                (ResourceKind.Wood, 10), (ResourceKind.Stone, 2)));

            ResourceAmounts cost = ResourceAmounts.Of(
                (ResourceKind.Wood, 5), (ResourceKind.Stone, 8));

            bool paid = pool.TrySpend(cost, out ResourceShortage shortage);

            Assert.That(paid, Is.False);
            Assert.That(pool[ResourceKind.Wood], Is.EqualTo(10), "nada pode ser debitado numa recusa");
            Assert.That(pool[ResourceKind.Stone], Is.EqualTo(2));
            Assert.That(shortage.Any, Is.True);
            Assert.That(shortage.Kind, Is.EqualTo(ResourceKind.Stone));
            Assert.That(shortage.Missing, Is.EqualTo(6));
        }

        [Test]
        public void CustoQueCabe_EPagoPorInteiro()
        {
            ResourcePool pool = new ResourcePool(ResourceAmounts.Of(
                (ResourceKind.Wood, 10), (ResourceKind.Stone, 8)));

            bool paid = pool.TrySpend(
                ResourceAmounts.Of((ResourceKind.Wood, 4), (ResourceKind.Stone, 3)),
                out ResourceShortage shortage);

            Assert.That(paid, Is.True);
            Assert.That(shortage.Any, Is.False);
            Assert.That(pool[ResourceKind.Wood], Is.EqualTo(6));
            Assert.That(pool[ResourceKind.Stone], Is.EqualTo(5));
        }

        [Test]
        public void FaltaEReportadaEmOrdemEstavel()
        {
            // Faltam dois recursos: a mensagem precisa ser sempre a mesma, ou a UI
            // pisca entre motivos diferentes para a mesma situacao.
            ResourcePool pool = new ResourcePool();
            ResourceAmounts cost = ResourceAmounts.Of(
                (ResourceKind.Stone, 5), (ResourceKind.Wood, 5));

            pool.CanAfford(cost, out ResourceShortage first);
            pool.CanAfford(cost, out ResourceShortage second);

            Assert.That(first.Kind, Is.EqualTo(second.Kind));
            Assert.That(first.Kind, Is.EqualTo(ResourceKind.Wood), "madeira vem antes de pedra na ordem");
        }

        [Test]
        public void CustoVazio_EsempreAceito()
        {
            ResourcePool pool = new ResourcePool();

            Assert.That(pool.TrySpend(new ResourceAmounts(), out ResourceShortage shortage), Is.True);
            Assert.That(shortage.Any, Is.False);
            Assert.That(pool.TrySpend(null, out _), Is.True);
        }

        [Test]
        public void AmountsSomamPorRecurso()
        {
            ResourceAmounts a = ResourceAmounts.Of((ResourceKind.Gold, 5), (ResourceKind.Food, 2));
            ResourceAmounts b = ResourceAmounts.Of((ResourceKind.Gold, 3));

            a.Add(b);

            Assert.That(a[ResourceKind.Gold], Is.EqualTo(8));
            Assert.That(a[ResourceKind.Food], Is.EqualTo(2));
        }

        [Test]
        public void AddDeAmountsNegativosDebita()
        {
            ResourcePool pool = new ResourcePool(ResourceAmounts.Of(ResourceKind.Food, 10));

            pool.Add(ResourceAmounts.Of(ResourceKind.Food, -4));

            Assert.That(pool[ResourceKind.Food], Is.EqualTo(6));
        }

        [Test]
        public void SnapshotNaoCompartilhaEstado()
        {
            ResourcePool pool = new ResourcePool(ResourceAmounts.Of(ResourceKind.Gold, 5));

            ResourceAmounts snapshot = pool.Snapshot();
            pool.Add(ResourceKind.Gold, 10);

            Assert.That(snapshot[ResourceKind.Gold], Is.EqualTo(5), "o retrato nao pode andar com o saldo");
            Assert.That(pool[ResourceKind.Gold], Is.EqualTo(15));
        }

        [Test]
        public void NonZeroSoListaOQueTemValor()
        {
            ResourceAmounts amounts = ResourceAmounts.Of(
                (ResourceKind.Wood, 3), (ResourceKind.Population, -1));

            System.Collections.Generic.List<ResourceKind> listed =
                new System.Collections.Generic.List<ResourceKind>(amounts.NonZero());

            Assert.That(listed.Count, Is.EqualTo(2));
            Assert.That(listed, Contains.Item(ResourceKind.Wood));
            Assert.That(listed, Contains.Item(ResourceKind.Population));
        }

        [Test]
        public void TextoDaFaltaNomeiaORecurso()
        {
            ResourceShortage shortage = new ResourceShortage(ResourceKind.Stone, 4);

            Assert.That(shortage.ToString(), Does.Contain("pedra"));
            Assert.That(shortage.ToString(), Does.Contain("4"));
        }
    }
}
