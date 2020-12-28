namespace RdfWorkflow.Test

open System
open Ductus.FluentDocker.Builders
open Xunit
open VDS.RDF.Query
open VDS.RDF.Update
open RdfWorkflow

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

    [<Fact>]
    member _.``Remote Update``() =
        let workflow = Utils.parseTurtleText  """
        @prefix w: <http://workflow.org/> .
        @prefix : <http://example.org/> .
        
        :w1 a w:Workflow ;
            w:startAt :s1 .
        :s1 a w:RemoteUpdateStep ;
            w:remoteEndpoint :db ;
            w:argumentsQuery "args.rq" ;
            w:sparqlUpdate "update.rq" ;
            w:next :ok .
        :ok a w:FinalStep ;
            w:success true .
        """
        let input = Utils.parseTurtleText """
            @prefix : <http://example.org/> . 

            :alice a :Person ; 
                :age 42 .
            :bob a :Person ;
                :age 41 .
        """
        let args = """
           prefix : <http://example.org/>
           SELECT ?person ?age WHERE {
             ?person a :Person ;
                :age ?age .
           }
           """
        let update = """
            prefix : <http://example.org/> 
            INSERT DATA { $person :age $age }
        """
        let config =  Utils.parseTurtleText """
            @prefix sd: <http://www.w3.org/ns/sparql-service-description#> .
            @prefix : <http://example.org/> .

            :db a sd:Service ;
                sd:endpoint <http://localhost:3030/test> .
        """

        let textResolver = Utils.resolve [ 
            "args.rq", args
            "update.rq", update ] 
        let w = Workflow(config, Steps([], textResolver), workflow)

        let response = w.Start(input)

        Assert.Equal(Status.Succeded, response.Status)
        Assert.Equal(2, response.StepNumber)
        Assert.Equal(Uri "http://example.org/ok", response.StepUri)


