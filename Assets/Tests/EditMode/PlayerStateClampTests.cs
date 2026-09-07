using NUnit.Framework;
using StudentGrowth;

namespace StudentGrowth.Tests
{
    public class PlayerStateClampTests
    {
        [Test]
        public void PressureDoesNotGoBelowZero()
        {
            PlayerState playerState = new PlayerState();

            playerState.SetStat(PlayerStatKey.Pressure, -10);

            Assert.AreEqual(0, playerState.pressure);
        }

        [Test]
        public void PressureDoesNotExceedOneHundred()
        {
            PlayerState playerState = new PlayerState();

            playerState.AddStat(PlayerStatKey.Pressure, 200);

            Assert.AreEqual(100, playerState.pressure);
        }

        [Test]
        public void EnergyDoesNotExceedMaxEnergy()
        {
            PlayerState playerState = new PlayerState();

            playerState.SetStat(PlayerStatKey.MaxEnergy, 40);
            playerState.SetStat(PlayerStatKey.Energy, 90);

            Assert.AreEqual(40, playerState.energy);
        }

        [Test]
        public void MoneyDoesNotGoBelowZero()
        {
            PlayerState playerState = new PlayerState();

            playerState.AddStat(PlayerStatKey.Money, -999);

            Assert.AreEqual(0, playerState.money);
        }
    }
}
