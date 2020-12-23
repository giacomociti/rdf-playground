As a software anarchitect I like to challenge the status quo:
I propose to use [RDF](https://www.w3.org/TR/rdf11-primer/)
and [SPARQL](https://www.w3.org/TR/sparql11-overview/)
for the core domain logic of business applications.

Many business applications consist of simple workflows to process rich information.
My claim is that RDF and SPARQL are ideal to model and process such information
while a workflow engine can orchestrate the processing steps.

OO classes _hide_ information. They do so to pursue modularity and tame complexity,
but [hiding information](https://en.wikipedia.org/wiki/Information_hiding) may not be
a good idea when building information processing applications.
More concrete structures like algebraic data types can model information explicitly and in fact are becoming popular for domain modeling.
Still, I contend that a logical framework like RDF shines at representing knowledge about a domain.

(see clojure)

As a proof of concept I hacked a toy workflow engine in a few lines of F# code.
It orchestrates the steps of workflow definitions like the following one (expressed as RDF in Turtle notation):

```ttl
@prefix w: <http://workflow.org/> .
@prefix : <http://example.org/> .

:search a w:Workflow ;
    w:startAt :validation .
:validation a w:AskStep ;
    w:sparqlQuery "validation.rq" ;
    w:nextOnTrue :retrieval ;
    w:nextOnFalse :ko .
:retrieval a w:ConstructStep ;
    w:sparqlQuery "retrieval.rq" ;
    w:next :ok .
:ko a w:FinalStep ;
    w:success false .
:ok a w:FinalStep ;
    w:success true .
```

The workflow accepts RDF input like:

```ttl
@prefix : <http://example.org/> .

[ a :SearchRequest ;
    :keyword "logic", "software" ] .
```

and the workflow steps use the dotNetRDF library to process information with SPARQL:
ASK queries for branching (although for validation we may also use something more specific like SHACL):

```sparql
prefix : <http://example.org/>

ASK
WHERE {
    ?request a :SearchRequest ;
        :keyword ?keyword .
    FILTER (strlen(?keyword) > 3)
}
```

and CONSTRUCT queries to transform and merge information:

```sparql
prefix : <http://example.org/>

CONSTRUCT {
    ?result :about ?keyword .
}
WHERE {
    ?request a :SearchRequest ;
        :keyword ?keyword .
    SERVICE <https://mytriples/sparql> {
        ?result :about ?keyword .
    }
}
```

Query processing happens in memory but we can use also RDF databases (triplestores) for persistent storage of information.
Federated queries (with the SERVICE keyword) allow to relate information in memory with information stored in RDF databases.

Of course real applications interact with different kinds of databases and other infrastructure (queues, APIs...)
so our workflow engine needs to plug in custom adapter code for such interactions
(and for when data processing is complex enough and requires a real programming language).
But, overall, RDF provides a great data model with standard and uniform tools to process, persist and serialize information with no impedance mismatch.
