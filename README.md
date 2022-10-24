# quiu

quiu (pronounced as queue) is an attemp to create a working message broker.

**Note that quiu is not production ready.**

## Usage

```sh
$ quiu.cli --help

  data-server     Start the data server
  admin-server    Start the admin server
  help            Display more information on a specific command.
  version         Display version information.
```

## data-server

Starts the data server. The part you use to ingest and read data.

```sh
$ quiu.cli data-server --help

  --builtins      Add default channel with the specified Guids
  -h, --host      (Default: *) Server host address
  -p, --port      (Default: 27812) Server port
  -c, --config    Config file path
  --help          Display this help screen.
  --version       Display version information.
  ```

### Examples

Starts a data server using the config in the file `config.yaml`.

```sh
$ quiu.cli data-server -c config.yaml 
```

Starts a data server using the config in the file `config.yaml`, with two channels `65894249-a68a-4ab2-b33d-7d26dee6038a` and `65894249-a68a-4ab2-b33d-7d26dee6038b`.

```sh
$ quiu.cli data-server -c config.yaml --builtins 65894249-a68a-4ab2-b33d-7d26dee6038a,65894249-a68a-4ab2-b33d-7d26dee6038b 
```

## Ingesting data

To ingest data, send the data as the body of a `POST`request to `/channel/{guid}`, one item per line.

Ingest an item:

```sh
curl -X POST 'localhost:27812/channel/71d35017-e0a8-412a-b340-07215eead781' -d "my data"
```

Ingest more than one item:

```sh
curl -X POST 'localhost:27812/channel/71d35017-e0a8-412a-b340-07215eead781' -d "my data\nanotherdata"
```

## Getting data

To retrieve the data at `offset` send a `GET`request to the path `/channel/{guid}/{offset}`.

Example: get the item att offset 6.

```sh
curl -X GET 'http://localhost:27812/channel/71d35017-e0a8-412a-b340-07215eead781/6'
```

To get `count` items, starting at `offset` send a `GET`request to the path `/channel/{guid}/{offset}/{count}`.

Example: get 5 items starting at offset 6.

```sh
curl -X GET 'http://localhost:27812/channel/71d35017-e0a8-412a-b340-07215eead781/6/5'
```

## admin-server

Starts the admin server. The part you use to manage channels.

This functionality will be merged into specific paths in `data-server`.

## Config file

```yaml
# Data server endpoint
server_host: localhost
server_port: 27812

# Admin server endpoint
admin_server_host: localhost
admin_server_port: 2781

# Data directory
data_dir: $HOME/var/quiu/

# Preload all channels present in ${data_dir}
# The alternative is to load them as requested
autorecover_channels: true
```

## Storage

quiu uses [ZoneTree](https://github.com/koculu/ZoneTree) as the storage engine. Esch channel storage is located under the data directory `${data_dir}/channels`.
