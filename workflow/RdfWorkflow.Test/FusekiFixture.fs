namespace RdfWorkflow.Test

open System
open Ductus.FluentDocker.Builders
open Xunit
open VDS.RDF.Query
open VDS.RDF.Update

type FusekiFixture() =
    let service = 
        Builder()
            .UseContainer()
            .UseImage("stain/jena-fuseki")
            .WithName("fuseki-test")
            .ExposePort(3030, 3030)
            .WithEnvironment("ADMIN_PASSWORD=admin")
            .WithEnvironment("FUSEKI_DATASET_1=test")
            .WaitForHttp("http://localhost:3030", timeout = 10000L)
            .Build()
            .Start()

    member _.EndpointUri = Uri "http://localhost:3030/test"

    interface IDisposable with
        member _.Dispose() = service.Dispose()

[<CollectionDefinition("Fuseki collection")>]
type DynamoDbCollection() =
    interface ICollectionFixture<FusekiFixture>

[<Collection("Fuseki collection")>]
type FusekiTest(fixture: FusekiFixture) =
    [<Fact>]
    member _.``Can Read and Write Remote Endpoint``() =
        let update = """
        prefix : <http://example.org/>

        INSERT DATA {
            :alice :loves :bob .
            :bob :loves :alice .
        }
        """
        SparqlRemoteUpdateEndpoint(fixture.EndpointUri).Update(update)

        let query = """
        prefix : <http://example.org/>

        CONSTRUCT WHERE { ?s :loves ?o }
        """
        let graph = SparqlRemoteEndpoint(fixture.EndpointUri).QueryWithResultGraph(query)
        Assert.Equal(2, graph.Triples.Count)
        


