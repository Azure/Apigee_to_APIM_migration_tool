using ApigeeToAzureApimMigrationTool.Service.Transformations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ApigeeToApimMigrationTool.Test.TransformationTests
{
    public class AssignMessageTransformationTests
    {
        [Fact]
        public async Task Transform_AddHeaderPolicy_AddsHeader()
        {
            // Arrange
            var apigeePolicyElement = XElement.Parse(
                @"<AssignMessage continueOnError=""false"" enabled=""true"" name=""Add-Header-1"">
                    <Add>
                        <Headers>
                            <Header name=""header-1"">value-1</Header>
                        </Headers>
                    </Add>
                </AssignMessage>");

            var apigeePolicyName = "Add-Header-1";

            var sut = new AssignMessageTransformation();

            // Act
            var result = await sut.Transform(apigeePolicyElement, apigeePolicyName);

            // Assert
            Assert.Single(result);
            Assert.Equal("set-header", result.First().Name.LocalName);
            Assert.Equal("header-1", result.First().Attribute("name").Value);
            Assert.Equal("value-1", result.First().Element("value").Value);
        }

        [Fact]
        public async Task Transform_SetHeaderPolicy_AddsHeader()
        {
               // Arrange
            var apigeePolicyElement = XElement.Parse(
              @"<AssignMessage continueOnError=""false"" enabled=""true"" name=""Set-Header-1"">
                    <Set>
                        <Headers>
                            <Header name=""header-1"">value-1</Header>
                        </Headers>
                    </Set>
                </AssignMessage>");

            var apigeePolicyName = "Set-Header-1";

            var sut = new AssignMessageTransformation();

            // Act
            var result = await sut.Transform(apigeePolicyElement, apigeePolicyName);

            // Assert
            Assert.Single(result);
            Assert.Equal("set-header", result.First().Name.LocalName);
            Assert.Equal("override", result.First().Attribute("exists-action").Value);
            Assert.Equal("header-1", result.First().Attribute("name").Value);
            Assert.Equal("value-1", result.First().Element("value").Value);
        }

        [Fact]
        public async Task Transform_RemoveHeaderPolicy_RemovesHeader()
        {
            // Arrange
            var apigeePolicyElement = XElement.Parse(
              @"<AssignMessage continueOnError=""false"" enabled=""true"" name=""Remove-Header-1"">
                    <Remove>
                        <Headers>
                            <Header name=""header-1""/>
                        </Headers>
                    </Remove>
                </AssignMessage>");

            var apigeePolicyName = "Remove-Header-1";

            var sut = new AssignMessageTransformation();

            // Act
            var result = await sut.Transform(apigeePolicyElement, apigeePolicyName);

            // Assert
            Assert.Single(result);
            Assert.Equal("set-header", result.First().Name.LocalName);
            Assert.Equal("delete", result.First().Attribute("exists-action").Value);
            Assert.Equal("header-1", result.First().Attribute("name").Value);
        }

        [Fact]
        public async Task Transform_SetPayloadPolicyWithJson_SetsJsonBody()
        {
            // Arrange
            var apigeePolicyElement = XElement.Parse(
               @"<AssignMessage continueOnError=""false"" enabled=""true"" name=""Set-Body-1"">
                    <Set>
                        <Payload contentType=""application/json"">
                            {""key"": ""value""}
                        </Payload>
                    </Set>
                </AssignMessage>");

            var apigeePolicyName = "Set-Body-1";

            var sut = new AssignMessageTransformation();

            // Act
            var result = await sut.Transform(apigeePolicyElement, apigeePolicyName);

            // Assert
            Assert.Single(result);
            Assert.Equal("set-body", result.First().Name.LocalName);
            Assert.Equal("{\"key\":\"value\"}", result.First().Value);
        }
    }
}
