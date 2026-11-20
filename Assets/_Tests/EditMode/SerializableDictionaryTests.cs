using NUnit.Framework;
using UnityEngine;
using ProjectB.Meta;

namespace ProjectB.Tests.EditMode
{
    public class SerializableDictionaryTests
    {
        [System.Serializable]
        private class Wrapper
        {
            public SerializableDictionary<string, int> map = new SerializableDictionary<string, int>();
        }

        [Test]
        public void JsonRoundTrip_PreservesEntries()
        {
            var original = new Wrapper();
            original.map["hero_hp"] = 3;
            original.map["hero_damage"] = 5;

            string json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<Wrapper>(json);

            Assert.AreEqual(2, restored.map.Count);
            Assert.IsTrue(restored.map.TryGetValue("hero_hp", out int hpLevel));
            Assert.AreEqual(3, hpLevel);
            Assert.IsTrue(restored.map.TryGetValue("hero_damage", out int dmgLevel));
            Assert.AreEqual(5, dmgLevel);
        }

        [Test]
        public void CorruptedMismatchedCounts_DoesNotThrowAndTruncatesSafely()
        {
            const string corruptedJson = "{\"map\":{\"keys\":[\"a\",\"b\",\"c\"],\"values\":[10]}}";

            Wrapper restored = null;
            Assert.DoesNotThrow(() => restored = JsonUtility.FromJson<Wrapper>(corruptedJson));
            Assert.AreEqual(1, restored.map.Count);
            Assert.IsTrue(restored.map.TryGetValue("a", out int valA));
            Assert.AreEqual(10, valA);
            Assert.IsFalse(restored.map.ContainsKey("b"));
        }

        [Test]
        public void Remove_RebuildsIndicesCorrectly()
        {
            var dict = new SerializableDictionary<string, int>();
            dict["first"] = 1;
            dict["second"] = 2;
            dict["third"] = 3;

            Assert.IsTrue(dict.Remove("first"));
            Assert.IsFalse(dict.ContainsKey("first"));
            Assert.AreEqual(2, dict["second"]);
            Assert.AreEqual(3, dict["third"]);
        }
    }
}
