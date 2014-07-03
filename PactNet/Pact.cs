﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PactNet.Consumer;
using PactNet.Consumer.Mocks.MockService;
using PactNet.Provider;

namespace PactNet
{
    //TODO: Implement a Pact file broker
    //TODO: Allow specification of a Pact file path

    public class Pact : IPactConsumer, IPactProvider
    {
        private string _consumerName;
        private string _providerName;
        private IMockProviderService _mockProviderService;
        private const string PactFileDirectory = "C:/specs/pacts/";
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public string PactFilePath
        {
            get { return Path.Combine(PactFileDirectory, PactFileName); }
        }

        public string PactFileName
        {
            get { return String.Format("{0}-{1}.json", _consumerName, _providerName); }
        }

        public IPactConsumer ServiceConsumer(string consumerName)
        {
            _consumerName = consumerName;

            return this;
        }

        public IPactConsumer HasPactWith(string providerName)
        {
            _providerName = providerName;

            return this;
        }

        public IPactProvider ServiceProvider(string providerName)
        {
            _providerName = providerName;

            return this;
        }

        public IPactProvider HonoursPactWith(string consumerName, HttpClient client)
        {
            _consumerName = consumerName;

            var pactFileJson = File.ReadAllText(PactFilePath);
            var pactFile = JsonConvert.DeserializeObject<PactFile>(pactFileJson, _jsonSettings);

            pactFile.VerifyProvider(client);

            return this;
        }

        public IMockProviderService MockService(int port)
        {
            _mockProviderService = new MockProviderService(port);

            _mockProviderService.Start();

            return _mockProviderService;
        }

        public void Dispose()
        {
            PersistPactFile();

            _mockProviderService.Stop();
            _mockProviderService.Dispose();
        }

        private void PersistPactFile()
        {
            PactFile pactFile;

            try
            {
                var previousPactFileJson = File.ReadAllText(PactFilePath);
                pactFile = JsonConvert.DeserializeObject<PactFile>(previousPactFileJson, _jsonSettings);
            }
            catch (IOException ex)
            {
                if (ex.GetType() == typeof(DirectoryNotFoundException))
                {
                    Directory.CreateDirectory(PactFileDirectory);
                }

                pactFile = new PactFile
                {
                    Provider = new PactParty { Name = _providerName },
                    Consumer = new PactParty { Name = _consumerName }
                };
            }

            pactFile.Interactions = pactFile.Interactions ?? new List<PactInteraction>();
            pactFile.AddInteraction(_mockProviderService.DescribeInteraction());

            var pactFileJson = JsonConvert.SerializeObject(pactFile, _jsonSettings);

            File.WriteAllText(PactFilePath, pactFileJson);
        }
    }
}