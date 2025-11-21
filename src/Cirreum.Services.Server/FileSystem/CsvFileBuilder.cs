namespace Cirreum.FileSystem;

using CsvHelper;
using CsvHelper.Configuration;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

sealed class CsvFileBuilder : ICsvFileBuilder {

	public byte[] BuildFile<TRecord>(IEnumerable<TRecord> records, string delimiter = ",") {
		var writerConfig = new CsvConfiguration(CultureInfo.InvariantCulture) {
			Delimiter = delimiter
		};
		using (var memoryStream = new MemoryStream()) {
			using (var streamWriter = new StreamWriter(memoryStream)) {
				using (var csvWriter = new CsvWriter(streamWriter, writerConfig)) {
					csvWriter.WriteRecords(records);
				}
			}
			return memoryStream.ToArray();
		}
	}

	public byte[] BuildFile<TRecord>(IEnumerable<TRecord> records, CsvConfiguration configuration) {
		using (var memoryStream = new MemoryStream()) {
			using (var streamWriter = new StreamWriter(memoryStream)) {
				using (var csvWriter = new CsvWriter(streamWriter, configuration)) {
					csvWriter.WriteRecords(records);
				}
			}
			return memoryStream.ToArray();
		}
	}

	public byte[] BuildFile<TRecord, TClassMap>(IEnumerable<TRecord> records) where TClassMap : ClassMap<TRecord> {
		var config = new CsvConfiguration(CultureInfo.InvariantCulture) {
		};
		using (var memoryStream = new MemoryStream()) {
			using (var streamWriter = new StreamWriter(memoryStream)) {
				using (var csvWriter = new CsvWriter(streamWriter, config)) {
					csvWriter.Context.RegisterClassMap<TClassMap>();
					csvWriter.WriteRecords(records);
				}
			}
			return memoryStream.ToArray();
		}
	}

	public byte[] BuildFile<TRecord, TClassMap>(IEnumerable<TRecord> records, CsvConfiguration configuration) where TClassMap : ClassMap<TRecord> {
		using (var memoryStream = new MemoryStream()) {
			using (var streamWriter = new StreamWriter(memoryStream)) {
				using (var csvWriter = new CsvWriter(streamWriter, configuration)) {
					csvWriter.WriteRecords(records);
				}
			}
			return memoryStream.ToArray();
		}
	}

}