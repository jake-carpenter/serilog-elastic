# Serilog + Elasticsearch

An example of using Serilog with the older Elasticsearch (ES) sink for pre-8.0 versions of ES.

This is an example that utilizes a container for ES + Kibana and runs a simple random-number generating API that logs to ES as well as a console application that will repeatedly call the API and log the random number that was generated as well. This will allow us to fairly quickly generate logs and demonstrate the process of rolling over indices.

## Important notes

This is an approach used to create indices with the deprecated versions of Index Templates. This is because the newer composable template style is not supported by the Serilog sink. If you're going to use ES 8.0 or newer, you should switch to the newer sink they provide for Serilog.

## Run ES and Kibana

This may take a while, but will initialize ES and Kibana v7.17.23.

```bash
docker-compose up
```

## Configure ES

Configuration for this example will include:

1. Configure some global settings for ES that you may not want outside of this demo, but it will help us view the process.

1. Creating an Index Lifecycle Policy (ILM) for the logs that will quickly move through
their cycles to easily demonstrate the process.

1. Creating index templates for both the API project and the console app project.

1. Creating rolling indices for each application to automate new index creation which will allow old logs to be deleted

### Configure Global Settings

Once Kibana is up and running, navigate to <http://localhost:5601/app/dev_tools#/console>. This will allow us to run our commands against ES in the Kibana console.

Typically ILM polling is done every 10 minutes by ES. This means that indices will not roll over immediately after reaching their qualified conditions, but will only do so during the next polling interval. We're going to reduce this 10 minute polling interval to 30 seconds to speed up the process.

Run this command in the Kibana dev console:

```json
# Updates the poll interval so rollovers will happen faster
PUT _cluster/settings
{
  "transient": {
    "indices.lifecycle.poll_interval": "30s"
  }
}
```

### Create Index Lifecycle Policy

This demo ILM policy will rollover from the Hot phase after either 20 minutes or 100 documents have been written to the index. The index will then skip the Warm phase and go directly to the Cold phase 1 minute after rollover. It will also reduce the number replicas to 0 entering the Cold phase. Finally, the index will then be deleted after 10 minutes.

The initial index will be suffixed with `-000001` and will rollover to `-000002` and so on.

Run this command in the Kibana dev console to create the ILM policy:

```json
PUT _ilm/policy/dotnet-logs-ilm
{
  "policy": {
    "phases": {
      "hot": {
        "min_age": "0ms",
        "actions": {
          "set_priority": {
            "priority": 100
          },
          "rollover": {
            "max_age": "20m",
            "max_docs": 100
          }
        }
      },
      "cold": {
        "min_age": "1m",
        "actions": {
          "set_priority": {
            "priority": 0
          },
          "allocate": {
            "number_of_replicas": 0
          }
        }
      },
      "delete": {
        "min_age": "10m",
        "actions": {
          "delete": {
            "delete_searchable_snapshot": true
          }
        }
      }
    }
  }
}
```

### Create Index Templates

As the Dotnet projects are configured, the ES Serilog sink will actually generate index templates for us. However, it will unfortunately misconfigure them and break ILM policies even if we apply the ILM after creation.

Here are the commands to create both index templates, one for each Dotnet project:

```json
PUT /_template/demo-console-app-template
{
  "index_patterns": ["demo-console-app*"],
  "settings": {
    "index": {
      "refresh_interval": "5s",
      "lifecycle": {
        "name": "dotnet-logs-ilm",
        "rollover_alias": "demo-console-app"
      }
    }
  },
  "mappings": {
    "dynamic_templates": [
      {
        "numerics_in_fields": {
          "match_pattern": "regex",
          "path_match": """fields\.[\d+]$""",
          "mapping": {
            "norms": false,
            "index": true,
            "type": "text"
          }
        }
      },
      {
        "string_fields": {
          "mapping": {
            "norms": false,
            "index": true,
            "type": "text",
            "fields": {
              "raw": {
                "ignore_above": 256,
                "index": true,
                "type": "keyword"
              }
            }
          },
          "match_mapping_type": "string",
          "match": "*"
        }
      }
    ],
    "properties": {
      "message": {
        "index": true,
        "type": "text"
      },
      "exceptions": {
        "type": "nested",
        "properties": {
          "ExceptionMessage": {
            "type": "object",
            "properties": {
              "MemberType": {
                "type": "integer"
              }
            }
          },
          "StackTraceString": {
            "index": true,
            "type": "text"
          },
          "HResult": {
            "type": "integer"
          },
          "RemoteStackTraceString": {
            "index": true,
            "type": "text"
          },
          "RemoteStackIndex": {
            "type": "integer"
          },
          "Depth": {
            "type": "integer"
          }
        }
      }
    }
  }
}


PUT /_template/demo-api-template
{
  "index_patterns": ["demo-api*"],
  "settings": {
    "index": {
      "refresh_interval": "5s",
      "lifecycle": {
        "name": "dotnet-logs-ilm",
        "rollover_alias": "demo-api"
      }
    }
  },
  "mappings": {
    "dynamic_templates": [
      {
        "numerics_in_fields": {
          "match_pattern": "regex",
          "path_match": """fields\.[\d+]$""",
          "mapping": {
            "norms": false,
            "index": true,
            "type": "text"
          }
        }
      },
      {
        "string_fields": {
          "mapping": {
            "norms": false,
            "index": true,
            "type": "text",
            "fields": {
              "raw": {
                "ignore_above": 256,
                "index": true,
                "type": "keyword"
              }
            }
          },
          "match_mapping_type": "string",
          "match": "*"
        }
      }
    ],
    "properties": {
      "message": {
        "index": true,
        "type": "text"
      },
      "exceptions": {
        "type": "nested",
        "properties": {
          "ExceptionMessage": {
            "type": "object",
            "properties": {
              "MemberType": {
                "type": "integer"
              }
            }
          },
          "StackTraceString": {
            "index": true,
            "type": "text"
          },
          "HResult": {
            "type": "integer"
          },
          "RemoteStackTraceString": {
            "index": true,
            "type": "text"
          },
          "RemoteStackIndex": {
            "type": "integer"
          },
          "Depth": {
            "type": "integer"
          }
        }
      }
    }
  }
}
```

#### Explanation of these templates

* `index_patterns`: This is the pattern that the index will match. Because these are defined as either `demo-console-app*` or `demo-api*`, each time a new index is created with a new suffix, it will match this pattern and apply the settings and mappings to the new index.

* `settings.lifecycle`: This is where we are applying the ILM policy that we created. We are also applying a `rollover_alias` which defines the alias for the index that will be used to rollover to the next index. This will be further explained soon.

* `mappings`: This can largely be ignored if you're using Serilog. This is the standard mapping template that Serilog would apply if we allowed it to create the index template. It is designed to allow for dynamic field names and to rollup exception messages that can be heavily nested.

### Create Rolling Indices

If we allow Serilog to create it's index for us, it will create one exactly as we have defined in our `appsettings.json` for each application. However, because we want to use ILM policies, we will actually be writing to rollover indices and using the index name we defined as a **writable index alias**. This merely means that we can reference it like an index, but it only exists as an alias to the current rollover index.

To create the first index for each application, we will use the following commands:

```json
PUT /demo-console-app-000001
{
  "aliases": {
    "demo-console-app": {
      "is_write_index": true
    }
  }
}

PUT /demo-api-000001
{
  "aliases": {
    "demo-api": {
      "is_write_index": true
    }
  }
}
```

## Running the applications to generate logs

In separate terminals, run commands to start the API and the console app:

```bash
dotnet run --project Api
```

```bash
dotnet run --project App
```

You should see they are generating logs to the console, but Serilog should also be sending those messages to ES.

To view the current ILM status for the indices, you can run the following commands in the Kibana dev console:

```json
GET /demo-api/_ilm/explain
GET /demo-console-app/_ilm/explain
```

You can also view the Index Management page in Kibana here <http://localhost:5601/app/management/data/index_management/indices>

Over time, you should see that indices suffixed -000002 and so on will be created. First, they will rollover and both be in the Hot phase. About 1 minute later, an index that rolled over will move into the Cold phase. Finally, after 10 minutes, the index will be deleted.
