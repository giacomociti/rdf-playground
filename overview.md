# Semantic Graphs Primer

- a data model: [RDF](https://www.w3.org/TR/rdf11-primer/)

- its query language: [SPARQL](https://www.w3.org/TR/sparql11-overview/)

- ontologies, rules and constraints: [OWL, SHACL](http://spinrdf.org/shacl-and-owl.html)

## Resource Description Framework

a simple and flexible data model based on triples:

    Subject Predicate Object

    e.g. Ann loves Bob

- a triple is an atomic unit of knowledge (an elementary fact)

- a set of triples is just a (directed labeled) graph

## Graphs

TODO show a gruff image

Graphs can be easily merged (unlike tables and documents). This is useful for open linked data but also for enterprise data integration.

## Resources
Resources are things (either concrete or abstract) that can be identified as individual entities in some universe of discourse.

- Subjects are resources
- Predicates are resources too (yes)
- Objects are resources or literals

actually subjects and objects may also be blank (anonymous) nodes.

Resources are identified by IRIs (international URIs, not necessarily URLs).

## SPARQL

- a declarative query language (a mix of SQL and Prolog)
- with some niceties for graph navigation (property paths)

- Just see [SPARQL in 11 minutes](https://www.youtube.com/watch?v=FvGndkpa4K0)

## Schema 

to describe predicates and sets of resources (classes)
- we use regular triples:

        :title rdf:type rdf:Property
        :title rdfs:label "the title of a book"@en
        :title rdfs:domain :Book

- so we can go *meta*

        rdf:type rdf:type rdf:Property
        rdfs:label rdfs:label "a human readable label"
        rdfs:Class rdf:type rdfs:Class

    things may get mind bending but are logically sound

## Semantics
Semantics is about explaining some meaning in plain English (e.g. *rdfs:label*) as well as defining logical relations (e.g. *rdfs:subClassOf*) with precise formal meaning that can be exploited by rule engines.

    :Man rdfs:subClassOf :Mortal
    :Socrates rdf:type :Man
    => :Socrates rdf:type :Mortal

OWL builds on RDF Schema providing further reasoning.

## Constraints
In the open world of the semantic web

*everybody can say anything about everything*

but to build reliable applications we need precise data shapes and constraints (SHACL).

