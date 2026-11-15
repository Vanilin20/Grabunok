using System;
using System.Net.Http;
using Banda.Net;
using NUnit.Framework;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Banda.Tests
{
    /// <summary>Помилки сервісів Unity мають доходити до гравця людською мовою (нема інтернету, тайм-аут, ліміт запитів, невірний код).</summary>
    public sealed class RelayErrorsTests
    {
        private static void AssertHuman(string text)
        {
            Assert.That(text, Is.Not.Null.And.Not.Empty);
            Assert.That(text, Does.Not.Contain("Exception"), "без назв винятків");
            Assert.That(text, Does.Match("[а-яіїєґА-ЯІЇЄҐ]"), "українською");
        }

        [Test]
        public void NoInternet_IsHuman()
        {
            string a = RelayErrors.Describe(new HttpRequestException("x"));
            string b = RelayErrors.Describe(new RequestFailedException(CommonErrorCodes.TransportError, "x"));
            AssertHuman(a);
            Assert.That(a, Is.EqualTo(RelayErrors.NoInternet));
            Assert.That(b, Is.EqualTo(RelayErrors.NoInternet));
        }

        [Test]
        public void Timeout_IsHuman()
        {
            AssertHuman(RelayErrors.Describe(new TimeoutException()));
            Assert.That(RelayErrors.Describe(new TimeoutException()), Does.Contain("не відповів"));
            Assert.That(RelayErrors.Describe(new RequestFailedException(CommonErrorCodes.Timeout, "x")), Does.Contain("не відповів"));
        }

        [Test]
        public void RateLimit_IsHuman()
        {
            string a = RelayErrors.Describe(new RequestFailedException(CommonErrorCodes.TooManyRequests, "x"));
            string b = RelayErrors.Describe(new SessionException("x", SessionError.RateLimitExceeded, null));
            AssertHuman(a);
            AssertHuman(b);
            Assert.That(a, Does.Contain("Зачекай"));
            Assert.That(b, Does.Contain("Зачекай"));
        }

        [Test]
        public void WrongCode_IsHuman()
        {
            string a = RelayErrors.Describe(new SessionException("x", SessionError.SessionNotFound, null));
            string b = RelayErrors.Describe(new RequestFailedException(CommonErrorCodes.NotFound, "x"));
            AssertHuman(a);
            AssertHuman(b);
            Assert.That(a, Does.Contain("код"));
            Assert.That(b, Does.Contain("код"));
        }

        [Test]
        public void ProjectNotLinked_IsHuman()
        {
            string text = RelayErrors.Describe(new ServicesInitializationException("x"));
            Assert.That(text, Is.EqualTo(RelayErrors.NotLinked));
            AssertHuman(text);
        }

        [Test]
        public void WrappedAndUnknown_StillHuman()
        {
            AssertHuman(RelayErrors.Describe(new AggregateException(new HttpRequestException("x"))));
            AssertHuman(RelayErrors.Describe(new InvalidOperationException("щось дивне")));
            AssertHuman(RelayErrors.Describe(new RequestFailedException(12345, "x")));
        }
    }
}
