﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Securities;

namespace QuantConnect.Tests.Common.Securities
{
    [TestFixture]
    public class CashTests
    {
        [Test]
        public void ConstructorCapitalizedSymbol()
        {
            var cash = new Cash("lower", 0, 0);
            Assert.AreEqual("LOWER", cash.Symbol);
        }

        [Test]
        public void ConstructorSetsProperties()
        {
            const string symbol = "JPY";
            const int quantity = 1;
            const decimal conversionRate = 1.2m;
            var cash = new Cash(symbol, quantity, conversionRate);
            Assert.AreEqual(symbol, cash.Symbol);
            Assert.AreEqual(quantity, cash.Quantity);
            Assert.AreEqual(conversionRate, cash.ConversionRate);
        }

        [Test]
        public void ComputesValueInBaseCurrency()
        {
            const int quantity = 100;
            const decimal conversionRate = 1/100m;
            var cash = new Cash("JPY", quantity, conversionRate);
            Assert.AreEqual(1m, cash.ValueInBaseCurrency);
        }

        [Test]
        public void EnsureCurrencyDataFeedAddsSubscription()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            var cash = new Cash("JPY", quantity, conversionRate);

            var securities = new SecurityManager();
            securities.Add("A", Resolution.Minute);
            var subscriptions = new SubscriptionManager();
            subscriptions.Add(SecurityType.Equity, "A", Resolution.Minute);
            cash.EnsureCurrencyDataFeed(subscriptions, securities);
            Assert.AreEqual(1, subscriptions.Subscriptions.Count(x => x.Symbol == "USDJPY"));
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException), MatchType = MessageMatch.Contains, ExpectedMessage = "Please add subscription")]
        public void EnsureCurrencyDataFeedAddsSubscriptionThrowsWhenZeroSubscriptionsPresent()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            var cash = new Cash("JPY", quantity, conversionRate);

            var securities = new SecurityManager();
            var subscriptions = new SubscriptionManager();
            cash.EnsureCurrencyDataFeed(subscriptions, securities);
        }

        [Test]
        public void EnsureCurrencyDataFeedsAddsSubscriptionAtMinimumResolution()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            const Resolution minimumResolution = Resolution.Second;
            var cash = new Cash("JPY", quantity, conversionRate);

            var subscriptions = new SubscriptionManager();
            subscriptions.Add(SecurityType.Equity, "A", Resolution.Minute);
            subscriptions.Add(SecurityType.Equity, "B", minimumResolution);

            var securities = new SecurityManager();
            securities.Add("A", Resolution.Minute);
            securities.Add("B", minimumResolution);

            cash.EnsureCurrencyDataFeed(subscriptions, securities);
            Assert.AreEqual(minimumResolution, subscriptions.Subscriptions.Single(x => x.Symbol == "USDJPY").Resolution);
        }

        [Test]
        public void EnsureCurrencyDataFeedMarksIsCurrencyDataFeedForNewSubscriptions()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            var cash = new Cash("JPY", quantity, conversionRate);

            var subscriptions = new SubscriptionManager();
            subscriptions.Add(SecurityType.Forex, "A", Resolution.Minute);
            subscriptions.Add(SecurityType.Forex, "A", Resolution.Minute);

            var securities = new SecurityManager();
            securities.Add("A", Resolution.Minute);

            cash.EnsureCurrencyDataFeed(subscriptions, securities);
            var config = subscriptions.Subscriptions.Single(x => x.Symbol == "USDJPY");
            Assert.IsTrue(config.IsCurrencyConversionFeed);
        }

        [Test]
        public void EnsureCurrencyDataFeedDoesNotMarkIsCurrencyDataFeedForExistantSubscriptions()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            var cash = new Cash("JPY", quantity, conversionRate);

            var subscriptions = new SubscriptionManager();
            subscriptions.Add(SecurityType.Forex, "USDJPY", Resolution.Minute);

            var securities = new SecurityManager();
            securities.Add("USDJPY", Resolution.Minute);

            cash.EnsureCurrencyDataFeed(subscriptions, securities);
            var config = subscriptions.Subscriptions.Single(x => x.Symbol == "USDJPY");
            Assert.IsFalse(config.IsCurrencyConversionFeed);
        }

        [Test]
        public void UpdateModifiesConversionRateAsInvertedValue()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            var cash = new Cash("JPY", quantity, conversionRate);

            var securities = new SecurityManager();
            securities.Add("USDJPY", Resolution.Minute);

            var subscriptions = new SubscriptionManager();
            subscriptions.Add(SecurityType.Forex, "USDJPY", Resolution.Minute);

            // we need to get subscription index
            cash.EnsureCurrencyDataFeed(subscriptions, securities);

            var last = 120m;
            var data = new Dictionary<int, List<BaseData>>();
            data.Add(0, new List<BaseData>
            {
                new Tick(DateTime.Now, "USDJPY", last, 119.95m, 120.05m)
            });

            cash.Update(data);

            // jpy is inverted, so compare on the inverse
            Assert.AreEqual(1 / last, cash.ConversionRate);
        }

        [Test]
        public void UpdateModifiesConversionRate()
        {
            const int quantity = 100;
            const decimal conversionRate = 1 / 100m;
            var cash = new Cash("GBP", quantity, conversionRate);

            var securities = new SecurityManager();
            securities.Add("GBPUSD", Resolution.Minute);

            var subscriptions = new SubscriptionManager();
            subscriptions.Add(SecurityType.Forex, "GBPUSD", Resolution.Minute);

            // we need to get subscription index
            cash.EnsureCurrencyDataFeed(subscriptions, securities);

            var last = 1.5m;
            var data = new Dictionary<int, List<BaseData>>();
            data.Add(0, new List<BaseData>
            {
                new Tick(DateTime.Now, "GBPUSD", last, last*1.009m, last*0.009m)
            });

            cash.Update(data);

            // jpy is inverted, so compare on the inverse
            Assert.AreEqual(last, cash.ConversionRate);
        }
    }
}