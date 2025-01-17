// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using FluentAssertions;
using System.Linq;
using FluentAssertions.Extensions;
using Xunit;
using System.Numerics;

namespace Microsoft.DotNet.Interactive.Formatting.Tests
{
    public class PlainTextFormatterTests : FormatterTestBase
    {
        public class Objects : FormatterTestBase
        {
            [Fact]
            public void Null_references_are_indicated()
            {
                string value = null;

                value.ToDisplayString().Should().Be("<null>");
            }

            [Fact]
            public void It_emits_the_property_names_and_values_for_a_specific_type()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Widget>();

                var writer = new StringWriter();
                formatter.Format(new Widget { Name = "Bob" }, writer);

                var s = writer.ToString();
                s.Should().Contain("Name: Bob");
            }

            [Fact]
            public void It_emits_a_default_maximum_number_of_properties()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Dummy.DummyClassWithManyProperties>();

                var writer = new StringWriter();
                formatter.Format(new Dummy.DummyClassWithManyProperties(), writer);

                var s = writer.ToString();
                s.Should().Be("{ Dummy.DummyClassWithManyProperties: X1: 1, X2: 2, X3: 3, X4: 4, X5: 5, X6: 6, X7: 7, X8: 8, X9: 9, X10: 10, X11: 11, X12: 12, X13: 13, X14: 14, X15: 15, X16: 16, X17: 17, X18: 18, X19: 19, X20: 20, .. }");
            }

            [Fact]
            public void It_emits_a_configurable_maximum_number_of_properties()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Dummy.DummyClassWithManyProperties>();
                PlainTextFormatter.MaxProperties = 1;

                var writer = new StringWriter();
                formatter.Format(new Dummy.DummyClassWithManyProperties(), writer);

                var s = writer.ToString();
                s.Should().Be("{ Dummy.DummyClassWithManyProperties: X1: 1, .. }");
            }

            [Fact]
            public void When_Zero_properties_chosen_just_ToString_is_used()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Dummy.DummyClassWithManyProperties>();
                PlainTextFormatter.MaxProperties = 0;

                var writer = new StringWriter();
                formatter.Format(new Dummy.DummyClassWithManyProperties(), writer);

                var s = writer.ToString();
                s.Should().Be("Dummy.DummyClassWithManyProperties");
            }

            [Fact]
            public void When_Zero_properties_available_to_choose_just_ToString_is_used()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Dummy.DummyWithNoProperties>();

                var writer = new StringWriter();
                formatter.Format(new Dummy.DummyWithNoProperties(), writer);

                var s = writer.ToString();
                s.Should().Be("Dummy.DummyWithNoProperties");
            }

            [Fact]
            public void CreateForMembers_emits_the_specified_property_names_and_values_for_a_specific_type()
            {
                var formatter = PlainTextFormatter<SomethingWithLotsOfProperties>.CreateForMembers(
                    o => o.DateProperty,
                    o => o.StringProperty);

                var s = new SomethingWithLotsOfProperties
                {
                    DateProperty = DateTime.MinValue,
                    StringProperty = "howdy"
                }.ToDisplayString(formatter);

                s.Should().Contain("DateProperty: 0001-01-01 00:00:00Z");
                s.Should().Contain("StringProperty: howdy");
                s.Should().NotContain("IntProperty");
                s.Should().NotContain("BoolProperty");
                s.Should().NotContain("UriProperty");
            }

            [Fact]
            public void CreateForMembers_throws_when_an_expression_is_not_a_MemberExpression()
            {
                var ex = Assert.Throws<ArgumentException>(() => PlainTextFormatter<SomethingWithLotsOfProperties>.CreateForMembers(
                                                              o => o.DateProperty.ToShortDateString(),
                                                              o => o.StringProperty));

                ex.Message.Should().Contain("o => o.DateProperty.ToShortDateString()");
            }

            [Theory]
            [InlineData(typeof(Boolean), "False")]
            [InlineData(typeof(Byte), "0")]
            [InlineData(typeof(Decimal), "0")]
            [InlineData(typeof(Double), "0")]
            [InlineData(typeof(Guid), "00000000-0000-0000-0000-000000000000")]
            [InlineData(typeof(Int16), "0")]
            [InlineData(typeof(Int32), "0")]
            [InlineData(typeof(Int64), "0")]
            [InlineData(typeof(Single), "0")]
            [InlineData(typeof(UInt16), "0")]
            [InlineData(typeof(UInt32), "0")]
            [InlineData(typeof(UInt64), "0")]
            public void It_does_not_expand_properties_of_scalar_types(Type type, string expected)
            {
                var value = Activator.CreateInstance(type);

                value.ToDisplayString().Should().Be(expected);
            }

            [Fact]
            public void It_expands_properties_of_structs()
            {
                var id = new EntityId("the typename", "the id");

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(id.GetType());

                var formatted = id.ToDisplayString(formatter);

                formatted.Should()
                         .Contain("TypeName: the typename")
                         .And
                         .Contain("Id: the id");
            }

            [Fact]
            public void Anonymous_types_are_automatically_fully_formatted()
            {
                var ints = new[] { 3, 2, 1 };

                var obj = new { ints, count = ints.Length };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(obj.GetType());

                var output = obj.ToDisplayString(formatter);

                output.Should().Be("{ ints: [ 3, 2, 1 ], count: 3 }");
            }

            [Fact]
            public void Formatter_expands_properties_of_ExpandoObjects()
            {
                dynamic expando = new ExpandoObject();
                expando.Name = "socks";
                expando.Parts = null;

                var formatter = PlainTextFormatter.GetPreferredFormatterFor<ExpandoObject>();

                var expandoString = ((object) expando).ToDisplayString(formatter);

                expandoString.Should().Be("{ Name: socks, Parts: <null> }");
            }

            [Fact]
            public void When_a_property_throws_it_does_not_prevent_other_properties_from_being_written()
            {
                var log = new SomePropertyThrows().ToDisplayString();

                log.Should().Contain("Ok:");
                log.Should().Contain("Fine:");
                log.Should().Contain("PerfectlyFine:");
            }

            [Fact]
            public void When_a_property_throws_then_then_exception_is_written_in_place_of_the_property()
            {
                var log = new SomePropertyThrows().ToDisplayString();

                log.Should().Contain("NotOk: { System.Exception: ");
            }

            [Fact]
            public void Recursive_formatter_calls_do_not_cause_exceptions()
            {
                var widget = new Widget();
                widget.Parts = new List<Part> { new Part { Widget = widget } };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(widget.GetType());

                widget.Invoking(w => w.ToDisplayString(formatter)).Should().NotThrow();
            }

            [Fact]
            public void Formatter_does_not_expand_string()
            {
                var widget = new Widget
                {
                    Name = "hello"
                };
                widget.Parts = new List<Part> { new Part { Widget = widget } };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Widget>();

                // this should not throw
                var s = widget.ToDisplayString(formatter);

                s.Should()
                 .Contain("hello")
                 .And
                 .NotContain("{ h },{ e }");
            }

            [Fact]
            public void Static_fields_are_not_written()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Widget>();
                new Widget().ToDisplayString(formatter)
                            .Should().NotContain(nameof(SomethingAWithStaticProperty.StaticField));
            }

            [Fact]
            public void Static_properties_are_not_written()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Widget>();
                new Widget().ToDisplayString(formatter)
                            .Should().NotContain(nameof(SomethingAWithStaticProperty.StaticProperty));
            }

            [Fact]
            public void It_expands_fields_of_objects()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor<SomeStruct>();
                var today = DateTime.Today;
                var tomorrow = DateTime.Today.AddDays(1);
                var id = new SomeStruct
                {
                    DateField = today,
                    DateProperty = tomorrow
                };

                var output = id.ToDisplayString(formatter);

                output.Should().Contain("DateField: ");
                output.Should().Contain("DateProperty: ");
            }

            [Fact]
            public void Output_can_include_internal_fields()
            {
                var formatter = PlainTextFormatter<Node>.CreateForAnyObject(true);

                var node = new Node { Id = "5" };

                var output = node.ToDisplayString(formatter);

                output.Should().Contain("_id: 5");
            }

            [Fact]
            public void Output_does_not_include_autoproperty_backing_fields()
            {
                var formatter = PlainTextFormatter<Node>.CreateForAnyObject(true);

                var output = new Node().ToDisplayString(formatter);

                output.Should().NotContain("<Nodes>k__BackingField");
                output.Should().NotContain("<NodesArray>k__BackingField");
            }

            [Fact]
            public void Output_can_include_internal_properties()
            {
                var formatter = PlainTextFormatter<Node>.CreateForAnyObject(true);

                var output = new Node { Id = "6" }.ToDisplayString(formatter);

                output.Should().Contain("InternalId: 6");
            }

            [Fact]
            public void Tuple_values_are_formatted()
            {
                var tuple = Tuple.Create(123, "Hello", Enumerable.Range(1, 3));

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(tuple.GetType());

                var formatted = tuple.ToDisplayString(formatter);

                formatted.Should().Be("( 123, Hello, [ 1, 2, 3 ] )");
            }
            
            [Fact]
            public void ValueTuple_values_are_formatted()
            {
                var tuple = (123, "Hello", Enumerable.Range(1, 3));

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(tuple.GetType());

                var formatted = tuple.ToDisplayString(formatter);

                formatted.Should().Be("( 123, Hello, [ 1, 2, 3 ] )");
            }

            [Fact]
            public void Enums_are_formatted_using_their_names()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(FileAccess));

                var writer = new StringWriter();

                formatter.Format(FileAccess.ReadWrite, writer);

                writer.ToString().Should().Be("ReadWrite");
            }

            [Fact]
            public void TimeSpan_is_not_destructured()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(TimeSpan));

                var writer = new StringWriter();

                var timespan = 25.Milliseconds();

                formatter.Format(timespan, writer);

                writer.ToString().Should().Be(timespan.ToString());
            }

            [Fact]
            public void PlainTextFormatter_returns_plain_for_BigInteger()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(BigInteger));

                var writer = new StringWriter();

                var instance = BigInteger.Parse("78923589327589332402359");

                formatter.Format(instance, writer);

                writer.ToString()
                    .Should()
                    .Be("78923589327589332402359");
            }
        }

        public class Sequences : FormatterTestBase
        {
            [Fact]
            public void Formatter_truncates_expansion_of_ICollection()
            {
                var list = new List<string>();
                for (var i = 1; i < 11; i++)
                {
                    list.Add("number " + i);
                }

                Formatter.ListExpansionLimit = 4;

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(ICollection));

                var formatted = list.ToDisplayString(formatter);

                formatted.Contains("number 1").Should().BeTrue();
                formatted.Contains("number 4").Should().BeTrue();
                formatted.Should().NotContain("number 5");
                formatted.Should().Contain("6 more");
            }

            [Fact]
            public void Formatter_expands_IEnumerable()
            {
                var list = new List<string> { "this", "that", "the other thing" };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(list.GetType());

                var formatted = list.ToDisplayString(formatter);

                formatted.Should()
                         .Be("[ this, that, the other thing ]");
            }

            [Fact]
            public void Formatter_truncates_expansion_of_IDictionary()
            {
                var list = new Dictionary<string, int>();
                for (var i = 1; i < 11; i++)
                {
                    list.Add("number " + i, i);
                }

                Formatter.ListExpansionLimit = 4;

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(list.GetType());

                var formatted = list.ToDisplayString(formatter);

                formatted.Should().Contain("number 1");
                formatted.Should().Contain("number 4");
                formatted.Should().NotContain("number 5");
                formatted.Should().Contain("6 more");
            }

            [Fact]
            public void Formatter_truncates_expansion_of_IEnumerable()
            {
                Formatter.ListExpansionLimit = 3;

                var formatter = PlainTextFormatter.GetPreferredFormatterFor<IEnumerable<int>>();

                var formatted = InfiniteSequence().ToDisplayString(formatter);

                formatted.Should().Contain("[ number 9, number 9, number 9 ... (more) ]");

                static IEnumerable<string> InfiniteSequence()
                {
                    while (true)
                    {
                        yield return "number 9";
                    }
                }
            }

            [Fact]
            public void Formatter_iterates_IEnumerable_property_when_its_reflected_type_is_array()
            {
                var node = new Node
                {
                    Id = "1",
                    NodesArray =
                        new[]
                        {
                            new Node { Id = "1.1" },
                            new Node { Id = "1.2" },
                            new Node { Id = "1.3" },
                        }
                };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Node>();

                var output = node.ToDisplayString(formatter);

                output.Should().Contain("1.1");
                output.Should().Contain("1.2");
                output.Should().Contain("1.3");
            }

            [Fact]
            public void Formatter_iterates_IEnumerable_property_when_its_actual_type_is_an_array_of_objects()
            {
                var node = new Node
                {
                    Id = "1",
                    Nodes =
                        new[]
                        {
                            new Node { Id = "1.1" },
                            new Node { Id = "1.2" },
                            new Node { Id = "1.3" },
                        }
                };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor<Node>();

                var output = node.ToDisplayString(formatter);

                output.Should().Contain("1.1");
                output.Should().Contain("1.2");
                output.Should().Contain("1.3");
            }

            [Fact]
            public void Formatter_iterates_IEnumerable_property_when_its_actual_type_is_an_array_of_structs()
            {
                var ints = new[] { 1, 2, 3, 4, 5 };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor(ints.GetType());

                ints.ToDisplayString(formatter)
                    .Should()
                    .Be("[ 1, 2, 3, 4, 5 ]");
            }

            [Fact]
            public void Formatter_recursively_formats_types_within_IEnumerable()
            {
                var list = new List<Widget>
                {
                    new Widget { Name = "widget x" },
                    new Widget { Name = "widget y" },
                    new Widget { Name = "widget z" }
                };

                var formatter = PlainTextFormatter.GetPreferredFormatterFor<List<Widget>>();

                var formatted = list.ToDisplayString(formatter);

                formatted.Should().Be("[ { Microsoft.DotNet.Interactive.Formatting.Tests.Widget: Name: widget x, Parts: <null> }, { Microsoft.DotNet.Interactive.Formatting.Tests.Widget: Name: widget y, Parts: <null> }, { Microsoft.DotNet.Interactive.Formatting.Tests.Widget: Name: widget z, Parts: <null> } ]");
            }

            [Fact]
            public void Properies_of_System_Type_instances_are_not_expanded()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(Type));

                var writer = new StringWriter();

                formatter.Format(typeof(string), writer);

                writer.ToString()
                      .Should()
                      .Be("System.String");
            }

            [Fact]
            public void ReadOnlyMemory_of_char_is_formatted_like_a_string()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(ReadOnlyMemory<char>));

                ReadOnlyMemory<char> readOnlyMemory = "Hi!".AsMemory();

                var writer = new StringWriter();

                formatter.Format(readOnlyMemory, writer);

                writer.ToString().Should().Be("Hi!");
            }


            [Fact]
            public void ReadOnlyMemory_of_int_is_formatted_like_a_int_array()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(ReadOnlyMemory<int>));

                var readOnlyMemory = new ReadOnlyMemory<int>(new[] { 1, 2, 3 });

                var writer = new StringWriter();

                formatter.Format(readOnlyMemory, writer);

                writer.ToString().Should().Be("[ 1, 2, 3 ]");
            }

            [Fact]
            public void It_shows_null_items_in_the_sequence_as_null()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(object[]));

                var writer = new StringWriter();

                formatter.Format(new object[] { 1, null, 3 }, writer);

                writer.ToString().Should().Be("[ 1, <null>, 3 ]");
            }

            [Fact]
            public void Strings_with_escaped_sequences_are_preserved()
            {
                var formatter = PlainTextFormatter.GetPreferredFormatterFor(typeof(string));
                
                var value = "hola! \n \t \" \" ' ' the joy of escapes! and    white  space  ";

                var writer = new StringWriter();

                formatter.Format(value, writer);

                writer.ToString().Should().Be(value);
            }
        }
    }
}