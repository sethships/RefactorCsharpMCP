using BenchmarkDotNet.Attributes;
using RefactorCsharpMCP.Benchmarks.Config;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Extract Class refactoring performance.
/// Tests performance across different code sizes and extraction complexity.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ExtractClassBenchmarks
{
    private string _smallCode = string.Empty;
    private string _mediumCode = string.Empty;
    private ExtractClass _refactoring = null!;

    [GlobalSetup]
    public void Setup()
    {
        _refactoring = new ExtractClass();

        // Small code sample - simple field extraction
        _smallCode = @"
using System;

namespace TestNamespace
{
    public class Person
    {
        private string _firstName;
        private string _lastName;
        private int _age;
        private string _email;

        public Person(string firstName, string lastName, int age, string email)
        {
            _firstName = firstName;
            _lastName = lastName;
            _age = age;
            _email = email;
        }

        public string GetFullName()
        {
            return $""{_firstName} {_lastName}"";
        }

        public string GetContactInfo()
        {
            return $""{_email}"";
        }
    }
}";

        // Medium code sample - multiple fields extraction
        _mediumCode = @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class Employee
    {
        private string _firstName;
        private string _lastName;
        private string _email;
        private string _phone;
        private string _street;
        private string _city;
        private string _state;
        private string _zipCode;
        private string _department;
        private decimal _salary;
        private List<string> _skills;

        public Employee(
            string firstName,
            string lastName,
            string email,
            string phone,
            string street,
            string city,
            string state,
            string zipCode,
            string department,
            decimal salary)
        {
            _firstName = firstName;
            _lastName = lastName;
            _email = email;
            _phone = phone;
            _street = street;
            _city = city;
            _state = state;
            _zipCode = zipCode;
            _department = department;
            _salary = salary;
            _skills = new List<string>();
        }

        public string GetFullName() => $""{_firstName} {_lastName}"";

        public string GetAddress() => $""{_street}, {_city}, {_state} {_zipCode}"";

        public string GetContactInfo() => $""{_email}, {_phone}"";

        public void AddSkill(string skill) => _skills.Add(skill);

        public List<string> GetSkills() => _skills;
    }
}";
    }

    [Benchmark(Description = "Extract class in small file (~30 lines)")]
    public async Task ExtractClass_SmallFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _smallCode,
            className: "Person",
            newClassName: "ContactInfo",
            fieldNames: "_email",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }

    [Benchmark(Description = "Extract class in medium file (~60 lines)")]
    public async Task ExtractClass_MediumFile()
    {
        var result = await _refactoring.ExecuteAsync(
            _mediumCode,
            className: "Employee",
            newClassName: "Address",
            fieldNames: "_street,_city,_state,_zipCode",
            targetFramework: "net8.0");

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Refactoring failed: {result.ErrorMessage}");
        }
    }
}
