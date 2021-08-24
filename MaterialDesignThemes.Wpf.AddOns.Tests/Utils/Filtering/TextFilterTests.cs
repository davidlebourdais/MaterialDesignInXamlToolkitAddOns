using System.ComponentModel;
using System.Linq;
using MaterialDesignThemes.Wpf.AddOns.Utils.Filtering;
using NUnit.Framework;

namespace MaterialDesignThemes.Wpf.AddOns.Tests.Utils.Filtering
{
    public class TextFilterTests
    {
        private PropertyDescriptor[] _testDataProperties;
        
        [SetUp]
        public void Setup()
        {
            _testDataProperties = TypeDescriptor.GetProperties(typeof(TestData))
                                                .OfType<PropertyDescriptor>()
                                                .ToArray();
        }
        
        [Test]
        public void should_pass_when_filter_is_empty()
        {
            var testData = new TestData("Some good text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "");

            Assert.IsTrue(result);
        }

        [Test]
        public void should_pass_when_filter_matches()
        {
            var testData = new TestData("Some good text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "good text");

            Assert.IsTrue(result);
        }

        [Test]
        public void should_fail_when_filter_does_not_match()
        {
           var testData = new TestData("Some good text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "wrong text");
            
            Assert.IsFalse(result);
        }
        
        [Test]
        public void should_not_ignore_case()
        {
            var testData = new TestData("Some TEXT");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "some text", ignoreCase: false);
            
            Assert.IsFalse(result);
        }
        
        [Test]
        public void should_ignore_case()
        {
            var testData = new TestData("Some TEXT");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "some text", ignoreCase: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_not_match_first_word_letters()
        {
            var testData = new TestData("Some text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So t", matchFilterWordsWithFirstWordLetters: false);
            
            Assert.IsFalse(result);
        }
        
        [Test]
        public void should_match_first_word_letters()
        {
            var testData = new TestData("Some text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So t", matchFilterWordsWithFirstWordLetters: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_not_match_first_word_letters_if_match_is_unordered()
        {
            var testData = new TestData("Some text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "t So", matchFilterWordsWithFirstWordLetters: true);
            
            Assert.IsFalse(result);
        }
        
        [Test]
        public void should_discard_spaces_when_matching_first_word_letters()
        {
            var testData = new TestData("Some     text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So t", matchFilterWordsWithFirstWordLetters: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_discard_non_alphanumerical_chars_when_matching_first_word_letters()
        {
            var testData = new TestData("Some (text)");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So t", matchFilterWordsWithFirstWordLetters: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_not_match_first_word_letters_across_members()
        {
            var testData = new TestData("Some crazy", " and incredible text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So text", matchFilterWordsFirstWordLettersAcrossProperties: false);
            
            Assert.IsFalse(result);
        }
        
        [Test]
        public void should_match_first_word_letters_members()
        {
            var testData = new TestData("Some crazy", " and incredible text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So text", matchFilterWordsFirstWordLettersAcrossProperties: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_match_all_word_letters_across_members()
        {
            var testData = new TestData("Some crazy", " and incredible text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "Some text", matchFilterWordsFirstWordLettersAcrossProperties: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_match_first_word_letters_across_members_unordered()
        {
            var testData = new TestData("Some crazy", " and incredible text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "text So", matchFilterWordsFirstWordLettersAcrossProperties: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_not_match_other_word_letters_across_members()
        {
            var testData = new TestData("Some crazy", " and incredible text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "me crazy and", matchFilterWordsFirstWordLettersAcrossProperties: true);
            
            Assert.IsFalse(result);
        }
        
        [Test]
        public void should_discard_spaces_when_matching_first_word_letters_across_members()
        {
            var testData = new TestData("Some   ", "  text");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So t", matchFilterWordsFirstWordLettersAcrossProperties: true);
            
            Assert.IsTrue(result);
        }
        
        [Test]
        public void should_discard_non_alphanumerical_chars_when_matching_first_word_letters_across_members()
        {
            var testData = new TestData("Some", "(text)");

            var result = TextFilter.IsItemMatchingFilter(testData, _testDataProperties, "So t", matchFilterWordsFirstWordLettersAcrossProperties: true);
            
            Assert.IsTrue(result);
        }
        
        private class TestData
        {
            public string Data1 { get; }
            public string Data2 { get; }
            public TestData(string data1, string data2 = "")
            {
                Data1 = data1;
                Data2 = data2;
            }
        }
    }
}
